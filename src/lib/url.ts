export function domainSlugFromUrl(url: string): string | undefined {
  try {
    const u = new URL(url);
    const host = u.hostname.toLowerCase();
    const parts = host.split(".").filter(Boolean);
    if (parts.length >= 2) return parts[parts.length - 2];
    if (parts.length === 1) return parts[0] === "www" ? undefined : parts[0];
    return undefined;
  } catch { return undefined; }
}
