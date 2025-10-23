/**
 * 列出 Roxy Browser 中所有可用的窗口配置
 */

import 'dotenv/config';
import { ConfigProvider } from './dist/config/ConfigProvider.js';
import { ServiceContainer } from './dist/core/container.js';

async function listWindows() {
  console.log('🔍 查询 Roxy Browser 中的窗口配置\n');

  try {
    const configProvider = ConfigProvider.load();
    const config = configProvider.getConfig();
    const container = new ServiceContainer(config);
    const roxyClient = container.createRoxyClient();

    // 调用 listWindows API
    console.log('📋 正在获取窗口列表...\n');
    const response = await roxyClient.listWindows({ page: 1, pageSize: 100 });

    console.log('✅ 获取成功！\n');
    console.log('API 响应:');
    console.log(JSON.stringify(response, null, 2));

    if (response.data && response.data.length > 0) {
      console.log(`\n共找到 ${response.data.length} 个窗口配置:\n`);
      response.data.forEach((item, index) => {
        console.log(`${index + 1}. DirId: ${item.dirId}`);
        console.log(`   名称: ${item.name || '(未命名)'}`);
        console.log(`   备注: ${item.remark || '(无备注)'}`);
        console.log('');
      });

      console.log('\n💡 请使用上述 DirId 之一进行测试\n');
    } else {
      console.log('\n⚠️  未找到任何窗口配置');
      console.log('💡 请在 Roxy Browser 中创建窗口配置后重试\n');
    }

  } catch (error) {
    console.error('❌ 查询失败:', error.message);
    if (error.context) {
      console.error('\n错误上下文:');
      console.error(JSON.stringify(error.context, null, 2));
    }
    process.exit(1);
  }
}

listWindows();
