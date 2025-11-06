export type ErrorCode =
  | "INVALID_INPUT"
  | "CONNECTION_FAILED"
  | "NAVIGATE_FAILED"
  | "NAVIGATE_TIMEOUT"
  | "LOCATOR_FAILED"
  | "LOCATOR_NOT_FOUND"
  | "ELEMENT_NOT_INTERACTABLE"
  | "ACTION_INTERCEPTED"
  | "ACTION_FAILED"
  | "SCREENSHOT_FAILED"
  | "TIMEOUT"
  | "INTERNAL_ERROR";

export interface ErrorShape {
  ok: false;
  code: ErrorCode | string;
  message?: string;
  data?: any;
}

export const err = (code: ErrorCode | string, message?: string, data?: any): ErrorShape => ({ ok: false, code, message, data });

export function mapError(e: any, ctx: "navigate" | "locator" | "action", data?: any): ErrorShape {
  const msg = String(e?.message ?? e);
  if (ctx === "navigate") {
    if (/timeout/i.test(msg)) return err("NAVIGATE_TIMEOUT", msg, data);
    return err("NAVIGATE_FAILED", msg, data);
  }
  if (ctx === "locator") {
    if (/not\s*found|no\s*node|unable\s*to\s*locate/i.test(msg)) return err("LOCATOR_NOT_FOUND", msg, data);
    if (/timeout/i.test(msg)) return err("TIMEOUT", msg, data);
    return err("LOCATOR_FAILED", msg, data);
  }
  // action
  if (/not\s*visible|not\s*interactable|detached|not\s*receiving/i.test(msg)) return err("ELEMENT_NOT_INTERACTABLE", msg, data);
  if (/blocked|intercept|overlay|modal|dialog/i.test(msg)) return err("ACTION_INTERCEPTED", msg, data);
  if (/timeout/i.test(msg)) return err("TIMEOUT", msg, data);
  return err("ACTION_FAILED", msg, data);
}
