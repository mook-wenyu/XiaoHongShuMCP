import { describe, it, expect } from 'vitest'
import { domainSlugFromUrl } from '../src/selectors/config.js'

describe('domainSlugFromUrl', () => {
  it('extracts second-level domain as slug', () => {
    expect(domainSlugFromUrl('https://www.xiaohongshu.com/explore')).toBe('xiaohongshu')
    expect(domainSlugFromUrl('https://m.zhihu.com/')).toBe('zhihu')
    expect(domainSlugFromUrl('https://xiaohongshu.com/')).toBe('xiaohongshu')
  })

  it('returns undefined on invalid url', () => {
    expect(domainSlugFromUrl('not-a-url')).toBeUndefined()
  })
})
