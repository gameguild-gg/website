/** Always run synchronous cleanup and retain both failures when both stages fail. */
export function runWithCleanup(operation, cleanup) {
  const failures = [];
  try {
    operation();
  } catch (error) {
    failures.push(error);
  }
  try {
    cleanup();
  } catch (error) {
    failures.push(error);
  }
  if (failures.length === 1) throw failures[0];
  if (failures.length > 1) {
    throw new AggregateError(failures, "Operation and cleanup both failed");
  }
}
