/* Lab 08 - Task 1: Verify MPI and Run Hello World
 * Every rank reports its rank, the communicator size and the processor name.
 * The unordered lines are printed as soon as each rank reaches them.
 * The ordered report is built by sending one line per rank to rank 0,
 * which prints them in rank order.
 *
 * Compile: mpicc -O2 -Wall -o hello_mpi hello_mpi.c
 * Run:     mpiexec -n <ranks> ./hello_mpi
 */
#include <stdio.h>
#include <string.h>
#include <mpi.h>

#define REPORT_TAG 1
#define LINE_LEN 512

int main(int argc, char *argv[])
{
    int rank, size, name_len;
    char processor_name[MPI_MAX_PROCESSOR_NAME];
    char line[LINE_LEN];

    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);
    MPI_Get_processor_name(processor_name, &name_len);

    snprintf(line, LINE_LEN, "Rank %d of %d on %s", rank, size, processor_name);
    printf("[unordered] %s\n", line);
    fflush(stdout);

    if (rank != 0) {
        MPI_Send(line, (int)strlen(line) + 1, MPI_CHAR, 0, REPORT_TAG, MPI_COMM_WORLD);
    } else {
        printf("\n=== Ordered report (collected by rank 0 in rank order) ===\n");
        printf("%s\n", line);
        for (int src = 1; src < size; src++) {
            MPI_Recv(line, LINE_LEN, MPI_CHAR, src, REPORT_TAG, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            printf("%s\n", line);
        }
        fflush(stdout);
    }

    MPI_Finalize();
    return 0;
}
