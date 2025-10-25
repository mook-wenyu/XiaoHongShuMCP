import { describe, it, expect } from 'vitest';

// 该用例不跑浏览器，仅验证 API 封装的导出存在与接口稳定。
// 重点：waitFeed/waitHomefeed/waitSearchNotes 默认采用 "okOnly:true" 的策略（实现细节：内部调用 waitApi 时传入 okOnly）。

import * as netwatch from '../../../src/domain/xhs/netwatch';

describe('netwatch exports', () => {
  it('should export waitFeed/waitHomefeed/waitSearchNotes', () => {
    expect(typeof netwatch.waitFeed).toBe('function');
    expect(typeof netwatch.waitHomefeed).toBe('function');
    expect(typeof netwatch.waitSearchNotes).toBe('function');
  });
});
