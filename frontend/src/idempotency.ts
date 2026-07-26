export function createIdempotencyKey(): string {
  return `console-${crypto.randomUUID()}`;
}
