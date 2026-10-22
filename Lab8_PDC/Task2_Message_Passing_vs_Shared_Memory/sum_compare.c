/* Lab 08 - Task 2: Message Passing vs Shared Memory: the Same Sum Two Ways
 * The same sum of N integers is computed three ways:
 *   collective     - MPI_Scatter the data, sum locally, MPI_Reduce the partial sums
 *   point-to-point - rank 0 MPI_Send each block, workers MPI_Send partial sums back
 *   shared memory  - OpenMP parallel reduction, one thread per rank
 * Every variant is timed REPEATS times and the average is reported in ms.
 *
 * Compile: mpicc -O2 -Wall -fopenmp -o sum_compare sum_compare.c
 * Run:     mpiexec -n <ranks> ./sum_compare      (the rank count must divide N)
 */
#include <stdio.h>
#include <stdlib.h>
#include <omp.h>
#include <mpi.h>

#define N 10000000
#define REPEATS 3
#define TAG_BLOCK 10
#define TAG_PARTIAL 11

static long long sum_block(const int *block, int count)
{
    long long sum = 0;
    for (int i = 0; i < count; i++)
        sum += block[i];
    return sum;
}

/* Collective variant: every rank gets one block, the root gets the total. */
static long long collective_sum(const int *full, int *local, int count)
{
    long long partial, total = 0;

    MPI_Scatter(full, count, MPI_INT, local, count, MPI_INT, 0, MPI_COMM_WORLD);
    partial = sum_block(local, count);
    MPI_Reduce(&partial, &total, 1, MPI_LONG_LONG, MPI_SUM, 0, MPI_COMM_WORLD);
    return total;
}

/* Point-to-point variant: rank 0 sends each block and collects the partial sums. */
static long long point_to_point_sum(const int *full, int *local, int count, int rank, int size)
{
    if (rank == 0) {
        long long total = sum_block(full, count);

        for (int dst = 1; dst < size; dst++)
            MPI_Send(full + (size_t)dst * count, count, MPI_INT, dst, TAG_BLOCK, MPI_COMM_WORLD);

        for (int src = 1; src < size; src++) {
            long long partial;
            MPI_Recv(&partial, 1, MPI_LONG_LONG, src, TAG_PARTIAL, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            total += partial;
        }
        return total;
    }

    MPI_Recv(local, count, MPI_INT, 0, TAG_BLOCK, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
    long long partial = sum_block(local, count);
    MPI_Send(&partial, 1, MPI_LONG_LONG, 0, TAG_PARTIAL, MPI_COMM_WORLD);
    return 0;
}

/* Shared-memory variant (rank 0 only): OpenMP threads, one per rank. */
static long long shared_sum(const int *full, int threads)
{
    long long total = 0;

    #pragma omp parallel for num_threads(threads) reduction(+:total)
    for (int i = 0; i < N; i++)
        total += full[i];
    return total;
}

int main(int argc, char *argv[])
{
    int rank, size;
    double collective_ms = 0.0, p2p_ms = 0.0, shared_ms = 0.0;
    int correct = 1;

    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    if (N % size != 0) {
        if (rank == 0)
            fprintf(stderr, "N=%d is not divisible by %d ranks\n", N, size);
        MPI_Abort(MPI_COMM_WORLD, 1);
    }

    int count = N / size;
    int *full = NULL;
    int *local = malloc((size_t)count * sizeof(int));
    long long reference = (long long)N * (N - 1) / 2;   /* sum of 0 .. N-1 */

    if (rank == 0) {
        full = malloc((size_t)N * sizeof(int));
        for (int i = 0; i < N; i++)
            full[i] = i;
    }

    for (int r = 0; r < REPEATS; r++) {
        double t0, t1;
        long long c_sum, p_sum, s_sum = 0;

        MPI_Barrier(MPI_COMM_WORLD);
        t0 = MPI_Wtime();
        c_sum = collective_sum(full, local, count);
        t1 = MPI_Wtime();
        if (rank == 0) collective_ms += (t1 - t0) * 1000.0;

        MPI_Barrier(MPI_COMM_WORLD);
        t0 = MPI_Wtime();
        p_sum = point_to_point_sum(full, local, count, rank, size);
        t1 = MPI_Wtime();
        if (rank == 0) p2p_ms += (t1 - t0) * 1000.0;

        if (rank == 0) {
            t0 = MPI_Wtime();
            s_sum = shared_sum(full, size);
            t1 = MPI_Wtime();
            shared_ms += (t1 - t0) * 1000.0;

            if (c_sum != reference || p_sum != reference || s_sum != reference)
                correct = 0;
        }
    }

    if (rank == 0) {
        printf("Ranks %d | threads %d | N %d | reference sum %lld\n",
               size, size, N, reference);
        printf("%-6s %-16s %-20s %-16s %s\n",
               "Ranks", "collective(ms)", "point-to-point(ms)", "shared(ms)", "correct");
        printf("%-6d %-16.2f %-20.2f %-16.2f %s\n",
               size, collective_ms / REPEATS, p2p_ms / REPEATS, shared_ms / REPEATS,
               correct ? "Y" : "N");
    }

    free(local);
    free(full);
    MPI_Finalize();
    return 0;
}
