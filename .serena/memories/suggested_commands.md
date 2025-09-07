# 开发命令和工作流

## 常用开发命令

### 构建和运行
```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行开发模式
dotnet run --project XiaoHongShuMCP

# 运行生产模式
dotnet run --project XiaoHongShuMCP --configuration Release

# 实时开发模式（文件变化自动重启）
dotnet watch --project XiaoHongShuMCP
```

### 测试相关
```bash
# 运行所有测试
dotnet test Tests

# 运行特定测试类
dotnet test Tests --filter "ClassName=SelectorManagerTests"

# 详细测试输出
dotnet test Tests --verbosity normal

# 生成测试报告
dotnet test Tests --logger trx --results-directory TestResults

# 生成测试覆盖报告
dotnet test Tests --collect:"XPlat Code Coverage"
```

### 发布部署
```bash
# 发布 Windows 版本
dotnet publish -c Release -r win-x64 --self-contained

# 发布 macOS 版本
dotnet publish -c Release -r osx-x64 --self-contained

# 发布 Linux 版本
dotnet publish -c Release -r linux-x64 --self-contained
```

## Windows 系统命令
由于项目运行在 Windows 环境：
```cmd
# 文件操作
dir          # 列出目录内容
cd           # 切换目录
type         # 查看文件内容
findstr      # 搜索文件内容

# Git 操作
git status
git add .
git commit -m "message"
git push

# 进程管理
tasklist     # 查看进程
taskkill     # 结束进程
```

## 任务完成检查清单
1. ✅ 代码编译无错误
2. ✅ 所有单元测试通过
3. ✅ 代码符合项目规范
4. ✅ 添加必要的日志记录
5. ✅ 更新相关文档
6. ✅ 性能测试通过