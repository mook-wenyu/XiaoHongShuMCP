/* 中文注释：概率分布函数（正态/均匀/泊松） */
export function randomNormal(mean = 0, stdDev = 1): number {
  // Box-Muller
  let u1 = 0, u2 = 0
  // 避免 log(0)
  while (u1 === 0) u1 = Math.random()
  u2 = Math.random()
  const z0 = Math.sqrt(-2 * Math.log(u1)) * Math.cos(2 * Math.PI * u2)
  return mean + z0 * stdDev
}

export function randomUniform(min = 0, max = 1): number {
  return min + Math.random() * (max - min)
}

export function randomPoisson(lambda = 1): number {
  const L = Math.exp(-lambda)
  let k = 0, p = 1
  do { k++; p *= Math.random() } while (p > L)
  return k - 1
}
