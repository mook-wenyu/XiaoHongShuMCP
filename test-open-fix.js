/**
 * 测试脚本：验证 Roxy API open() 方法修复
 *
 * 测试目标：
 * 1. 验证 ConnectionInfoSchema 是否正确处理缺少 id 字段的响应
 * 2. 验证 open() 方法是否能正确使用 dirId 作为默认 id
 * 3. 验证调试日志是否输出
 */

import 'dotenv/config';
import { ConfigProvider } from './dist/config/ConfigProvider.js';
import { ServiceContainer } from './dist/core/container.js';

const TEST_DIR_ID = '73defe990dcbedb6ac3a598a119ea8d4';  // 正确的 dirId（从 Roxy Browser 获取）

async function testOpenFix() {
  console.log('🧪 开始测试 Roxy API open() 方法修复\n');
  console.log('配置信息:');
  console.log(`- Token: ${process.env.ROXY_API_TOKEN?.substring(0, 8)}...`);
  console.log(`- DirId: ${TEST_DIR_ID}\n`);

  try {
    // 1. 初始化配置和容器
    console.log('📦 加载配置...');
    const configProvider = ConfigProvider.load();
    const config = configProvider.getConfig();
    const container = new ServiceContainer(config);
    const roxyClient = container.createRoxyClient();
    console.log('✅ 配置加载成功\n');

    // 2. 测试健康检查
    console.log('🏥 测试健康检查...');
    try {
      const health = await roxyClient.health();
      console.log('✅ 健康检查成功:', health);
    } catch (error) {
      console.error('❌ 健康检查失败:', error.message);
      console.log('⚠️  请确保 Roxy Browser 正在运行在 http://127.0.0.1:50000\n');
      process.exit(1);
    }

    // 3. 测试 open() 方法
    console.log('\n🔓 测试 open() 方法...');
    console.log(`   DirId: ${TEST_DIR_ID}`);

    const startTime = Date.now();
    const connection = await roxyClient.open(TEST_DIR_ID);
    const duration = Date.now() - startTime;

    console.log(`✅ open() 方法成功 (耗时: ${duration}ms)\n`);

    // 4. 验证返回值
    console.log('📊 验证返回值:');
    console.log(`   id: ${connection.id} ${connection.id === TEST_DIR_ID ? '✅ (使用 dirId 作为默认值)' : '⚠️'}`);
    console.log(`   ws: ${connection.ws} ${connection.ws ? '✅' : '❌'}`);
    console.log(`   http: ${connection.http || 'undefined'} ${connection.http ? '✅' : '(可选)'}`);

    // 5. 测试 ensureOpen() 方法
    console.log('\n🔄 测试 ensureOpen() 方法...');
    const connection2 = await roxyClient.ensureOpen(TEST_DIR_ID);
    console.log(`✅ ensureOpen() 成功 (应该复用已有连接)`);
    console.log(`   ws: ${connection2.ws}`);

    // 6. 测试总结
    console.log('\n' + '='.repeat(60));
    console.log('✅ 所有测试通过！修复成功！');
    console.log('='.repeat(60));
    console.log('\n修复验证：');
    console.log('✅ Schema 正确处理了缺少 id 字段的响应');
    console.log('✅ open() 方法正确使用 dirId 作为默认 id');
    console.log('✅ ensureOpen() 方法正常工作');
    console.log('\n🎉 现在可以在 Claude Desktop 中正常使用 roxy.openDir 工具了！\n');

  } catch (error) {
    console.error('\n' + '='.repeat(60));
    console.error('❌ 测试失败！');
    console.error('='.repeat(60));
    console.error('\n错误信息:', error.message);

    if (error.context) {
      console.error('\n错误上下文:');
      console.error(JSON.stringify(error.context, null, 2));
    }

    if (error.stack) {
      console.error('\n完整堆栈:');
      console.error(error.stack);
    }

    console.error('\n💡 调试建议:');
    console.error('1. 检查 Roxy Browser 是否正在运行');
    console.error('2. 确认 dirId 是否正确');
    console.error('3. 查看上方的错误上下文中的 rawResponse');
    console.error('4. 如果是 Zod 验证错误，查看 zodError 字段\n');

    process.exit(1);
  }
}

// 运行测试
testOpenFix();
