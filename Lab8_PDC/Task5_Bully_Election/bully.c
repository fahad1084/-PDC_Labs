/* Lab 08 - Task 5 (optional): Bully Leader Election
 * Ranks are priorities: the highest surviving rank becomes the leader.
 *
 * Usage: mpiexec -n <ranks> ./bully [crashed_rank] [initiator]
 *   crashed_rank default = size-1 (the current leader)
 *   initiator    default = 0
 *
 * Message types:
 *   ELECTION    - "I am starting an election, higher ranks please answer"
 *   ANSWER      - "I am alive and higher than you, I take over"
 *   COORDINATOR - "rank X is the new leader"
 *
 * A crashed rank never answers: it only swallows incoming messages. Survivors
 * detect it through a timeout: if no higher rank answers ANSWER_TIMEOUT seconds
 * after the ELECTION, the sender declares itself leader.
 *
 * Compile: mpicc -O2 -Wall -o bully bully.c
 * Run:     mpiexec -n 5 ./bully
 *          mpiexec -n 5 ./bully 2 0
 */
#include <stdio.h>
#include <stdlib.h>
#include <mpi.h>

#define TAG_ELECTION 30
#define TAG_ANSWER 31
#define TAG_COORDINATOR 32

#define ANSWER_TIMEOUT 0.10       /* seconds to wait for an ANSWER */
#define COORDINATOR_TIMEOUT 1.00  /* seconds to wait for a COORDINATOR after an ANSWER */
#define DRAIN_WINDOW 0.30         /* survivors keep answering after they know the leader */
#define CRASH_WINDOW 1.50         /* a crashed rank silently swallows messages this long */

typedef struct {
    int rank, size, crashed;
    int leader;               /* -1 until a COORDINATOR is known */
    int in_election, got_answer;
    double election_start;
    int messages_sent;
} Node;

static const char *tag_name(int tag)
{
    switch (tag) {
    case TAG_ELECTION: return "ELECTION";
    case TAG_ANSWER: return "ANSWER";
    case TAG_COORDINATOR: return "COORDINATOR";
    default: return "?";
    }
}

static void send_msg(Node *nd, int dst, int tag, int payload)
{
    MPI_Send(&payload, 1, MPI_INT, dst, tag, MPI_COMM_WORLD);
    nd->messages_sent++;
    printf("  rank %d -> rank %d : %s(%d)\n", nd->rank, dst, tag_name(tag), payload);
    fflush(stdout);
}

static void become_leader(Node *nd)
{
    nd->leader = nd->rank;
    nd->in_election = 0;
    printf("rank %d: no higher rank answered, I am the leader\n", nd->rank);
    for (int j = 0; j < nd->rank; j++)
        send_msg(nd, j, TAG_COORDINATOR, nd->rank);
}

static void start_election(Node *nd)
{
    nd->in_election = 1;
    nd->got_answer = 0;
    nd->election_start = MPI_Wtime();
    printf("rank %d: starts an election\n", nd->rank);

    if (nd->rank == nd->size - 1) {
        become_leader(nd);
        return;
    }
    for (int j = nd->rank + 1; j < nd->size; j++)
        send_msg(nd, j, TAG_ELECTION, nd->rank);
}

/* Handles one received message. Called in the election loop and in the drain window. */
static void handle_message(Node *nd, int src, int tag, int payload)
{
    switch (tag) {
    case TAG_ELECTION:
        if (nd->leader >= 0) {
            send_msg(nd, src, TAG_COORDINATOR, nd->leader);
        } else {
            send_msg(nd, src, TAG_ANSWER, nd->rank);
            if (!nd->in_election)
                start_election(nd);
        }
        break;
    case TAG_ANSWER:
        nd->got_answer = 1;
        break;
    case TAG_COORDINATOR:
        nd->leader = payload;
        nd->in_election = 0;
        printf("rank %d: new leader is rank %d\n", nd->rank, payload);
        fflush(stdout);
        break;
    }
}

/* Receives one pending message if there is one. Returns 1 if a message was handled. */
static int poll_once(Node *nd)
{
    int flag;
    MPI_Status status;
    MPI_Iprobe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &flag, &status);
    if (!flag)
        return 0;

    int payload;
    MPI_Recv(&payload, 1, MPI_INT, status.MPI_SOURCE, status.MPI_TAG,
             MPI_COMM_WORLD, MPI_STATUS_IGNORE);
    handle_message(nd, status.MPI_SOURCE, status.MPI_TAG, payload);
    return 1;
}

static void run_survivor(Node *nd, int initiator)
{
    if (nd->rank == initiator)
        start_election(nd);

    while (nd->leader < 0) {
        if (poll_once(nd))
            continue;

        double now = MPI_Wtime();
        if (nd->in_election && !nd->got_answer && now - nd->election_start > ANSWER_TIMEOUT) {
            become_leader(nd);
        } else if (nd->in_election && nd->got_answer && now - nd->election_start > COORDINATOR_TIMEOUT) {
            start_election(nd);
        }
    }

    /* Keep answering late ELECTION messages so that no message is left unreceived. */
    double end = MPI_Wtime() + DRAIN_WINDOW;
    while (MPI_Wtime() < end)
        poll_once(nd);
}

static void run_crashed(int rank)
{
    double end = MPI_Wtime() + CRASH_WINDOW;
    printf("rank %d: CRASHED (silent, no answers)\n", rank);
    fflush(stdout);

    while (MPI_Wtime() < end) {
        int flag;
        MPI_Status status;
        MPI_Iprobe(MPI_ANY_SOURCE, MPI_ANY_TAG, MPI_COMM_WORLD, &flag, &status);
        if (flag) {
            int discarded;
            MPI_Recv(&discarded, 1, MPI_INT, status.MPI_SOURCE, status.MPI_TAG,
                     MPI_COMM_WORLD, MPI_STATUS_IGNORE);
        }
    }
}

int main(int argc, char *argv[])
{
    Node nd;
    int leaders_seen_by_rank0 = -1;

    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &nd.rank);
    MPI_Comm_size(MPI_COMM_WORLD, &nd.size);

    nd.crashed = (argc > 1) ? atoi(argv[1]) : nd.size - 1;
    int initiator = (argc > 2) ? atoi(argv[2]) : 0;

    if (nd.size < 3 || nd.crashed < 0 || nd.crashed >= nd.size ||
        initiator < 0 || initiator >= nd.size || initiator == nd.crashed) {
        if (nd.rank == 0)
            fprintf(stderr, "Usage: mpiexec -n <ranks>=3+> %s [crashed_rank] [initiator]\n", argv[0]);
        MPI_Finalize();
        return 1;
    }

    nd.leader = -1;
    nd.in_election = 0;
    nd.got_answer = 0;
    nd.election_start = 0.0;
    nd.messages_sent = 0;

    if (nd.rank == 0)
        printf("Bully: ranks %d | crashed rank %d | initiator %d\n", nd.size, nd.crashed, initiator);

    if (nd.rank == nd.crashed)
        run_crashed(nd.rank);
    else
        run_survivor(&nd, initiator);

    /* Gather what every rank decided. The crashed rank contributes -1. */
    int my_view = (nd.rank == nd.crashed) ? -1 : nd.leader;
    int *views = NULL;
    if (nd.rank == 0)
        views = malloc((size_t)nd.size * sizeof(int));
    MPI_Gather(&my_view, 1, MPI_INT, views, 1, MPI_INT, 0, MPI_COMM_WORLD);

    int total_messages = 0;
    MPI_Reduce(&nd.messages_sent, &total_messages, 1, MPI_INT, MPI_SUM, 0, MPI_COMM_WORLD);

    if (nd.rank == 0) {
        int first_alive = (nd.crashed == 0) ? 1 : 0;
        int agree = 1;
        leaders_seen_by_rank0 = views[first_alive];

        for (int r = 0; r < nd.size; r++) {
            if (r == nd.crashed)
                continue;
            if (views[r] != leaders_seen_by_rank0)
                agree = 0;
        }

        printf("RESULT: ranks %d | crashed %d | initiator %d | new leader %d | all_agree=%s | messages %d\n",
               nd.size, nd.crashed, initiator, leaders_seen_by_rank0,
               agree ? "Y" : "N", total_messages);
        free(views);
    }

    MPI_Barrier(MPI_COMM_WORLD);
    MPI_Finalize();
    return 0;
}
