export const APP_VERSION = process.env.NEXT_PUBLIC_APP_VERSION ?? '1.8.0';

function parseVersion(value: string | null | undefined): number[] {
  if (!value) return [0, 0, 0];

  return value
    .trim()
    .replace(/^v/i, '')
    .split('-')[0]
    .split('.')
    .map((part) => Number.parseInt(part, 10) || 0)
    .concat([0, 0, 0])
    .slice(0, 3);
}

export function isVersionOlder(
  currentVersion: string | null | undefined,
  requiredVersion: string | null | undefined,
): boolean {
  if (!currentVersion || !requiredVersion) return false;

  const current = parseVersion(currentVersion);
  const required = parseVersion(requiredVersion);

  for (let index = 0; index < 3; index += 1) {
    if (current[index] !== required[index]) {
      return current[index] < required[index];
    }
  }

  return false;
}
