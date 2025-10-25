# services/policy.ts — 深度分析

## 角色
- 策略调度器：令牌桶限速 + 熔断（open/half-open/closed）+ 连续失败阈值。

## 核心
- qps→perTokenMs；`acquire()` 中 refill + await perTokenMs；
- 失败计数达到 `failureThreshold` → 状态 open，持续 `openSeconds`；
- open 状态等待至 until 再半开；半开成功回 closed；失败保持/再开。

## 建议
- acquire() 中的限速循环对超高并发可能延迟较多，可选加入批量 token 填充或动态 backoff。
- 记录失败时增加错误分组（error.name + message hash），便于报表去噪。

## 文件参考
- src/services/policy.ts:1
