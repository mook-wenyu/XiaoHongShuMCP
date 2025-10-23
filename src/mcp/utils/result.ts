/* 中文注释：统一的 ActionResult 与错误处理工具 */

export interface ActionErrorInfo {
  code: string;
  message: string;
  attempts?: number;
  timings?: Record<string, number>;
  lastLocator?: string;
  candidates?: string[];
  screenshotPath?: string;
}

export interface ActionResult<T = any> {
  ok: boolean;
  value?: T;
  error?: ActionErrorInfo;
}

export function ok<T>(value?: T): ActionResult<T> { return { ok: true, value }; }
export function fail(info: ActionErrorInfo): ActionResult { return { ok: false, error: info }; }
