## 韧性与节律（Resilience × Pacing）设计说明（中文）

### 目标
- 降低 429/403 与风控命中率；
- 将网络往返时间（RTT）与限流令牌成本联动，形成“自适应节律”；
- 出错快速失败（写端熔断），读端平滑退避；
- 可观测：以低基数标签输出 OTel 指标，便于看板与告警。

### 架构要点
- `PacingAdvisor`：维护 `multiplier`（倍率因子），输入：RTT 观测、HTTP 状态（429/403），输出：读写端 permits 建议；
- `RateLimitingRateLimiter`：基于 `System.Threading.RateLimiting` 令牌桶，写端 `permits = ceil(multiplier)`，读端 `permits = round(multiplier/2)`；
- `PollyCircuitBreakerAdapter`：打开时对写端快速拒绝（Fail-Fast），并计数 `circuit_open_total`；
- `UniversalApiMonitor`：以 Request/Response 事件差估算 `uam_rtt_ms{endpoint}`，回馈至 `PacingAdvisor`，形成闭环。

### 关键指标（OTel）
- `rate_limit_acquired_total{endpoint,permits,multiplier}`：成功获取令牌次数；
- `rate_limit_wait_ms{endpoint}`：获取令牌等待时长；
- `uam_total/2xx/429/403{endpoint}`：UAM 事件统计；
- `uam_rtt_ms{endpoint}`：RTT 直方图；
- `circuit_open_total`：熔断打开计数；
- `human_delay_ms{wait_type,multiplier}`：人类化延时采样。

### 策略细节
- RTT→multiplier：指数滑动平均（EMA）平滑，基础窗口 10s；
- 403/429：倍率上限翻倍并追加冷却期；
- 限流标签基数控制：`endpoint` 采用离散字典（见 EndpointClassifier），`permits/multiplier` 取整裁剪。

### 使用指引
- 所有外部调用走 `ResiliencePipelines` 包装；
- 写端操作必须先查询熔断状态；
- 指标导出建议使用 OTLP，采样率按 5% 起步并配合异常全量上报。
