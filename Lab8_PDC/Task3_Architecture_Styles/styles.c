/* Lab 08 - Task 3: Design Note: MPI Deployments and Architecture Styles
 * master : task farm (client-server style). Rank 0 is the server that hands out
 *          tasks; ranks 1 .. size-1 are clients that request work and return results.
 * ring   : token ring (peer-to-peer style). Every rank runs the same code and
 *          passes a token to its neighbour; rank 0 only starts each lap.
 *
 * Compile: mpicc -O2 -Wall -o styles styles.c
 * Run:     mpiexec -n <ranks> ./styles master
 *          mpiexec -n <ranks> ./styles ring
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <mpi.h>

#define NUM_TASKS 60
#define LAPS 1000
#define MODULUS 1000003

#define TAG_WORK 1
#define TAG_RESULT 2
#define TAG_STOP 3
#define TAG_TOKEN 4

/* Task id -> problem size. Both the server and the clients can compute it. */
static int task_size(int id)
{
    return 1000 + id * 50;
}

/* The work done for one task: a sum of squares modulo a prime. */
static long long work_unit(int n)
{
    long long acc = 0;
    for (int k = 1; k <= n; k++)
        acc = (acc + (long long)k * k) % MODULUS;
    return acc;
}

static void run_worker(void)
{
    int id;
    MPI_Status status;

    for (;;) {
        MPI_Recv(&id, 1, MPI_INT, 0, MPI_ANY_TAG, MPI_COMM_WORLD, &status);
        if (status.MPI_TAG == TAG_STOP)
            break;

        long long reply[2] = { id, work_unit(task_size(id)) };
        MPI_Send(reply, 2, MPI_LONG_LONG, 0, TAG_RESULT, MPI_COMM_WORLD);
    }
}

static void run_master(int size)
{
    long long *expected = malloc(NUM_TASKS * sizeof(long long));
    int *served = calloc(size, sizeof(int));
    int next_task = 0, finished = 0, correct = 1;
    int dummy = 0;

    for (int i = 0; i < NUM_TASKS; i++)
        expected[i] = work_unit(task_size(i));

    /* Give every worker one task, or a STOP if there are fewer tasks than workers. */
    for (int w = 1; w < size; w++) {
        if (next_task < NUM_TASKS) {
            MPI_Send(&next_task, 1, MPI_INT, w, TAG_WORK, MPI_COMM_WORLD);
            served[w]++;
            next_task++;
        } else {
            MPI_Send(&dummy, 1, MPI_INT, w, TAG_STOP, MPI_COMM_WORLD);
        }
    }

    /* Hand the next task to whichever worker answers first. */
    while (finished < NUM_TASKS) {
        long long reply[2];
        MPI_Status status;

        MPI_Recv(reply, 2, MPI_LONG_LONG, MPI_ANY_SOURCE, TAG_RESULT, MPI_COMM_WORLD, &status);
        int w = status.MPI_SOURCE;
        int id = (int)reply[0];

        if (reply[1] != expected[id])
            correct = 0;
        finished++;

        if (next_task < NUM_TASKS) {
            MPI_Send(&next_task, 1, MPI_INT, w, TAG_WORK, MPI_COMM_WORLD);
            served[w]++;
            next_task++;
        } else {
            MPI_Send(&dummy, 1, MPI_INT, w, TAG_STOP, MPI_COMM_WORLD);
        }
    }

    /* Workers that were never given a task were stopped in the first loop. */
    printf("Task farm: ranks %d | workers %d | tasks %d\n", size, size - 1, NUM_TASKS);
    for (int w = 1; w < size; w++)
        printf("  worker %d served %d tasks\n", w, served[w]);

    int total = 0;
    for (int w = 1; w < size; w++)
        total += served[w];
    printf("Tasks served in total: %d | correct=%s\n", total, correct && total == NUM_TASKS ? "Y" : "N");

    free(expected);
    free(served);
}

static void run_ring(int rank, int size)
{
    int next = (rank + 1) % size;
    int prev = (rank - 1 + size) % size;
    long long token = 0;

    MPI_Barrier(MPI_COMM_WORLD);
    double t0 = MPI_Wtime();

    if (rank == 0) {
        for (int lap = 0; lap < LAPS; lap++) {
            MPI_Send(&token, 1, MPI_LONG_LONG, next, TAG_TOKEN, MPI_COMM_WORLD);
            MPI_Recv(&token, 1, MPI_LONG_LONG, prev, TAG_TOKEN, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            token++;
        }
    } else {
        for (int lap = 0; lap < LAPS; lap++) {
            MPI_Recv(&token, 1, MPI_LONG_LONG, prev, TAG_TOKEN, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            token++;
            MPI_Send(&token, 1, MPI_LONG_LONG, next, TAG_TOKEN, MPI_COMM_WORLD);
        }
    }

    double t1 = MPI_Wtime();

    if (rank == 0) {
        long long messages = (long long)LAPS * size;
        double total_ms = (t1 - t0) * 1000.0;
        int correct = (token == messages);

        printf("Token ring: ranks %d | laps %d | messages %lld\n", size, LAPS, messages);
        printf("  time = %.3f ms | time per message = %.6f ms | final token %lld | correct=%s\n",
               total_ms, total_ms / messages, token, correct ? "Y" : "N");
    }
}

int main(int argc, char *argv[])
{
    int rank, size;

    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    if (argc < 2 || (strcmp(argv[1], "master") != 0 && strcmp(argv[1], "ring") != 0)) {
        if (rank == 0)
            fprintf(stderr, "Usage: mpiexec -n <ranks> %s master|ring\n", argv[0]);
        MPI_Finalize();
        return 1;
    }

    if (size < 2) {
        if (rank == 0)
            fprintf(stderr, "Run with at least 2 ranks\n");
        MPI_Finalize();
        return 1;
    }

    if (strcmp(argv[1], "master") == 0) {
        if (rank == 0)
            run_master(size);
        else
            run_worker();
    } else {
        run_ring(rank, size);
    }

    MPI_Finalize();
    return 0;
}
