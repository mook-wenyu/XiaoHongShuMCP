/* 统一的 API 监听封装（先挂监听→再触发）
 * 目标：高内聚、低耦合；避免各处分散 waitForResponse；便于超时/解析策略统一调整。
 */
import type { Page, Response } from "playwright";

export type ApiResult<T = any> = {
  ok: boolean;
  url?: string;
  status?: number;
  ttfbMs?: number;
  data?: T;
};

export type Waiter<T = any> = {
  promise: Promise<ApiResult<T>>;
};

type WaitOpts<T> = {
  timeoutMs: number;
  map?: (resp: Response) => Promise<ApiResult<T>>;
  okOnly?: boolean; // 仅首个 2xx 成功响应（默认 true），避免非 2xx 预检/错误回执提前结束
};

// 安全提取 Response 元信息（在测试 mock 无 ok/url/status 方法时不抛错）
function safeMeta(resp: any): { ok: boolean; url?: string; status?: number } {
  let ok = true;
  let url: string | undefined;
  let status: number | undefined;
  try { ok = typeof resp?.ok === "function" ? Boolean(resp.ok()) : true; } catch {}
  try { url = typeof resp?.url === "function" ? String(resp.url()) : undefined; } catch {}
  try { status = typeof resp?.status === "function" ? Number(resp.status()) : undefined; } catch {}
  return { ok, url, status };
}

export function waitApi<T = any>(page: Page, matcher: (r: Response) => boolean, opts: WaitOpts<T>): Waiter<T> {
  const started = Date.now();
  const predicate = (r: Response) => {
    try { return matcher(r) && (opts.okOnly !== false ? (typeof (r as any).ok === "function" ? (r as any).ok() : true) : true); } catch { return false; }
  };
  const promise = page.waitForResponse(predicate, { timeout: opts.timeoutMs })
    .then(async (resp): Promise<ApiResult<T>> => {
      const meta = safeMeta(resp as any);
      try {
        if (opts.map) return await opts.map(resp);
        return { ...meta, ttfbMs: Date.now() - started } as ApiResult<T>;
      } catch {
        return { ...meta, ok: false, ttfbMs: Date.now() - started } as ApiResult<T>;
      }
    })
    .catch((): ApiResult<T> => ({ ok: false }));
  return { promise };
}

// 小红书常用 API 封装
export function waitFeed(page: Page, timeoutMs: number): Waiter<{ items?: any[]; type?: string }> {
  return waitApi(page, r => r.url().includes("/api/sns/web/v1/feed"), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const items = Array.isArray(data?.data?.items) ? data.data.items : [];
      const first = items[0];
      const type = first?.note_card?.type || first?.type || undefined;
      return { ...meta, data: { items, type } };
    }
  });
}

export function waitHomefeed(page: Page, timeoutMs: number): Waiter<{ items?: any[] }> {
  return waitApi(page, r => r.url().includes("/api/sns/web/v1/homefeed"), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const items = Array.isArray(data?.data?.items) ? data.data.items : [];
      return { ...meta, data: { items } };
    }
  });
}

export type SearchItem = { id?: string; note_card?: { display_title?: string } };
export function waitSearchNotes(page: Page, timeoutMs: number): Waiter<{ items: SearchItem[] }> {
  return waitApi(page, r => r.url().includes("/api/sns/web/v1/search/notes"), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const raw = Array.isArray(data?.data?.items) ? data.data.items : [];
      const items: SearchItem[] = raw
        .map((it: any) => ({ id: it?.id, note_card: { display_title: it?.note_card?.display_title } }))
        .filter((x: any) => x.id || x.note_card?.display_title);
      return { ...meta, data: { items } };
    }
  });
}

// ========== 交互确认（点赞/收藏/关注） ==========
export function waitLike(page: Page, timeoutMs: number): Waiter<{ new_like?: boolean }> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/note\/like\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true);
      const new_like = data?.data?.new_like === true;
      return { ...meta, ok, data: { new_like } } as any;
    }
  });
}
export function waitDislike(page: Page, timeoutMs: number): Waiter<{}> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/note\/dislike\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true);
      return { ...meta, ok } as any;
    }
  });
}
export function waitCollect(page: Page, timeoutMs: number): Waiter<{}> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/note\/collect\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true);
      return { ...meta, ok } as any;
    }
  });
}
export function waitUncollect(page: Page, timeoutMs: number): Waiter<{}> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/note\/uncollect\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true);
      return { ...meta, ok } as any;
    }
  });
}
export function waitFollow(page: Page, timeoutMs: number): Waiter<{ fstatus?: string }> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/user\/follow\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true) && (data?.data?.fstatus === "follows");
      return { ...meta, ok, data: { fstatus: data?.data?.fstatus } } as any;
    }
  });
}
export function waitUnfollow(page: Page, timeoutMs: number): Waiter<{ fstatus?: string }> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/user\/unfollow\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true) && (data?.data?.fstatus === "none");
      return { ...meta, ok, data: { fstatus: data?.data?.fstatus } } as any;
    }
  });
}

// 发表评论：/api/sns/web/v1/comment/post
export function waitComment(page: Page, timeoutMs: number): Waiter<{ id?: string; note_id?: string; content?: string }> {
  return waitApi(page, r => /\/api\/sns\/web\/v1\/comment\/post\b/.test(r.url()), {
    timeoutMs,
    okOnly: true,
    map: async (resp) => {
      const meta = safeMeta(resp as any);
      let data: any; try { data = await (resp as any).json?.(); } catch { data = undefined; }
      const ok = (data?.code === 0 || data?.success === true);
      const id = data?.data?.comment?.id;
      const note_id = data?.data?.comment?.note_id;
      const content = data?.data?.comment?.content;
      return { ...meta, ok, data: { id, note_id, content } } as any;
    }
  });
}
