/* Lab 08 - Task 4 (optional): Lamport Logical Clock
 * Each rank keeps a Lamport clock. In every round it performs a local event,
 * a send to one partner and a receive from another partner. Every event is
 * logged with its clock value. Rank 0 gathers the logs, merges them, sorts by
 * (clock, rank) and checks every message against the clock condition
 * (send clock < receive clock) and every rank's log for strictly increasing clocks.
 *
 * Compile: mpicc -O2 -Wall -o lamport lamport.c
 * Run:     mpiexec -n <ranks> ./lamport
 */
#include <stdio.h>
#include <stdlib.h>
#include <mpi.h>

#define ROUNDS 4
#define EVENTS_PER_ROUND 3          /* local, send, receive */
#define EVENTS_PER_RANK (ROUNDS * EVENTS_PER_ROUND)
#define EV_LOCAL 0
#define EV_SEND 1
#define EV_RECV 2
#define TAG_MSG 20

static const char *kind_name[] = { "local", "send", "recv" };

/* Stored as plain ints so the whole array can be gathered with MPI_INT. */
typedef struct {
    int clock;          /* Lamport clock after the event */
    int rank;           /* rank that recorded the event */
    int kind;           /* EV_LOCAL, EV_SEND or EV_RECV */
    int peer;           /* destination (send) or source (recv), -1 for local */
    int msg_id;         /* sender * 1000 + round, -1 for local */
    int msg_clock;      /* clock carried by the message (recv only), else -1 */
    int clock_before;   /* clock before the event rule was applied */
} Event;

static int lamport_clock = 0;

static void record(Event *log, int *n, int rank, int kind, int peer,
                   int msg_id, int msg_clock, int clock_before)
{
    Event *e = &log[(*n)++];
    e->clock = lamport_clock;
    e->rank = rank;
    e->kind = kind;
    e->peer = peer;
    e->msg_id = msg_id;
    e->msg_clock = msg_clock;
    e->clock_before = clock_before;
}

static int by_clock_then_rank(const void *a, const void *b)
{
    const Event *x = a, *y = b;
    if (x->clock != y->clock)
        return x->clock - y->clock;
    return x->rank - y->rank;
}

int main(int argc, char *argv[])
{
    int rank, size, n = 0;
    Event log[EVENTS_PER_RANK];

    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    if (size < 2) {
        if (rank == 0)
            fprintf(stderr, "Run with at least 2 ranks\n");
        MPI_Finalize();
        return 1;
    }

    for (int round = 0; round < ROUNDS; round++) {
        /* A different partner each round: shift 1 .. size-1, so every send has a receive. */
        int shift = round % (size - 1) + 1;
        int dest = (rank + shift) % size;
        int src = (rank - shift + size) % size;
        int out[2], in[2];

        /* 1. Local event. */
        int before = lamport_clock;
        lamport_clock++;
        record(log, &n, rank, EV_LOCAL, -1, -1, -1, before);

        /* 2. Send event: stamp the message with the new clock. */
        before = lamport_clock;
        lamport_clock++;
        out[0] = lamport_clock;
        out[1] = rank * 1000 + round;
        record(log, &n, rank, EV_SEND, dest, out[1], -1, before);

        MPI_Sendrecv(out, 2, MPI_INT, dest, TAG_MSG,
                     in, 2, MPI_INT, src, TAG_MSG,
                     MPI_COMM_WORLD, MPI_STATUS_IGNORE);

        /* 3. Receive event: clock = max(local, message clock) + 1. */
        before = lamport_clock;
        lamport_clock = (before > in[0] ? before : in[0]) + 1;
        record(log, &n, rank, EV_RECV, src, in[1], in[0], before);
    }

    /* Gather every rank's log on rank 0 and merge it. */
    Event *all = NULL;
    if (rank == 0)
        all = malloc((size_t)size * EVENTS_PER_RANK * sizeof(Event));

    MPI_Gather(log, EVENTS_PER_RANK * (int)sizeof(Event) / (int)sizeof(int), MPI_INT,
               all, EVENTS_PER_RANK * (int)sizeof(Event) / (int)sizeof(int), MPI_INT,
               0, MPI_COMM_WORLD);

    if (rank == 0) {
        int total = size * EVENTS_PER_RANK;
        int messages = 0, violations = 0;

        /* Each rank's own events must have strictly increasing clocks. */
        for (int r = 0; r < size; r++) {
            Event *mine = &all[r * EVENTS_PER_RANK];
            for (int i = 1; i < EVENTS_PER_RANK; i++)
                if (mine[i].clock <= mine[i - 1].clock)
                    violations++;
        }

        /* Every receive must carry a clock smaller than the matching send's clock. */
        for (int i = 0; i < total; i++) {
            Event *recv = &all[i];
            if (recv->kind != EV_RECV)
                continue;
            messages++;
            for (int j = 0; j < total; j++) {
                Event *send = &all[j];
                if (send->kind == EV_SEND && send->msg_id == recv->msg_id) {
                    if (send->clock >= recv->clock)
                        violations++;
                    break;
                }
            }
        }

        qsort(all, total, sizeof(Event), by_clock_then_rank);

        FILE *out_file = fopen("lamport_merged.log", "w");
        fprintf(out_file, "%-6s %-6s %-7s %-6s %-8s %-10s %s\n",
                "clock", "rank", "kind", "peer", "msg_id", "msg_clock", "clock_before");
        for (int i = 0; i < total; i++) {
            Event *e = &all[i];
            fprintf(out_file, "%-6d %-6d %-7s %-6d %-8d %-10d %d\n",
                    e->clock, e->rank, kind_name[e->kind], e->peer,
                    e->msg_id, e->msg_clock, e->clock_before);
        }
        fclose(out_file);

        printf("Ranks %d | events %d | messages %d | causal violations %d | consistent=%s\n",
               size, total, messages, violations, violations == 0 ? "Y" : "N");
        printf("Merged log written to lamport_merged.log\n");

        /* Follow message 1: round 0, rank 0 -> rank 1. */
        for (int i = 0; i < total; i++) {
            Event *e = &all[i];
            if (e->kind == EV_RECV && e->msg_id == 0) {
                printf("Message 1: sender rank %d | send clock %d | receiver rank %d | "
                       "clock before receive %d | receive clock %d | rule max(%d,%d)+1\n",
                       0, e->msg_clock, e->rank, e->clock_before, e->clock,
                       e->clock_before, e->msg_clock);
                break;
            }
        }

        free(all);
    }

    MPI_Finalize();
    return 0;
}
