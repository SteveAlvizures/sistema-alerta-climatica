export function required(name: string, minLength = 1): string {
  const value = process.env[name];
  if (!value || value.length < minLength) throw new Error(`Missing or invalid ${name}`);
  return value;
}
