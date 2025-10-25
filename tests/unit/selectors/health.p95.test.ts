import { describe, it, expect, beforeEach } from 'vitest'
import { healthMonitor } from '../../../src/selectors/health.js'

describe('health monitor p95', () => {
  beforeEach(() => healthMonitor.clear())

  it('computes p95 from recent durations', () => {
    const id = 'sel.p95.test'
    // 1..100 ms
    for (let i=1;i<=100;i++) healthMonitor.record(id, true, i)
    const h = healthMonitor.getHealth(id)!
    expect(h.totalCount).toBe(100)
    expect(h.p95DurationMs).toBeDefined()
    // p95 of 1..100 ~= 95th element => 95
    expect(h.p95DurationMs).toBe(95)
  })
})
