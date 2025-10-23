/**
 * 列出 Roxy Browser 中所有工作空间和窗口配置
 */

import 'dotenv/config';
import { ConfigProvider } from './dist/config/ConfigProvider.js';
import { ServiceContainer } from './dist/core/container.js';

async function listAll() {
  console.log('🔍 查询 Roxy Browser 配置\n');

  try {
    const configProvider = ConfigProvider.load();
    const config = configProvider.getConfig();
    const container = new ServiceContainer(config);
    const roxyClient = container.createRoxyClient();

    // 1. 获取工作空间列表
    console.log('📋 正在获取工作空间列表...\n');
    const workspacesResponse = await roxyClient.workspaces({ page: 1, pageSize: 100 });

    console.log('工作空间 API 响应:');
    console.log(JSON.stringify(workspacesResponse, null, 2));

    const workspaces = workspacesResponse.data?.rows;

    if (!workspaces || workspaces.length === 0) {
      console.log('\n⚠️  未找到任何工作空间');
      console.log('💡 请在 Roxy Browser 中创建工作空间和窗口配置后重试\n');
      return;
    }

    console.log(`\n✅ 找到 ${workspaces.length} 个工作空间\n`);

    // 2. 遍历每个工作空间，获取窗口列表
    for (const workspace of workspaces) {
      console.log(`━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`);
      console.log(`工作空间: ${workspace.workspaceName} (ID: ${workspace.id})`);
      console.log(`━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n`);

      try {
        const windowsResponse = await roxyClient.listWindows({
          workspaceId: workspace.id,
          page: 1,
          pageSize: 100
        });

        const windows = windowsResponse.data?.rows || windowsResponse.data;

        if (windows && windows.length > 0) {
          console.log(`共 ${windows.length} 个窗口配置:\n`);
          windows.forEach((item, index) => {
            console.log(`  ${index + 1}. DirId: ${item.dirId}`);
            console.log(`     名称: ${item.name || '(未命名)'}`);
            console.log(`     备注: ${item.remark || '(无备注)'}`);
            console.log('');
          });
        } else {
          console.log('  (此工作空间下无窗口配置)\n');
        }
      } catch (error) {
        console.error(`  ❌ 获取窗口列表失败: ${error.message}\n`);
      }
    }

    console.log('\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
    console.log('💡 请使用上述 DirId 之一进行测试');
    console.log('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n');

  } catch (error) {
    console.error('❌ 查询失败:', error.message);
    if (error.context) {
      console.error('\n错误上下文:');
      console.error(JSON.stringify(error.context, null, 2));
    }
    process.exit(1);
  }
}

listAll();
