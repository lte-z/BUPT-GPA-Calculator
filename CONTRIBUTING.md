# 贡献指南

感谢你关注 BUPT GPA Calculator。项目以本地、可核对、轻量的成绩管理体验为目标。

## 开发环境

- Windows 10 或 Windows 11 x64；
- .NET SDK 10.0.109 或兼容的 10.0 SDK；
- PowerShell 与 Git。

```powershell
dotnet restore BuptGpaCalculator.sln
dotnet build BuptGpaCalculator.sln --configuration Release --no-restore
dotnet test BuptGpaCalculator.sln --configuration Release --no-build
```

## 分支与提交

- `main` 应始终保持可构建、可测试；
- 非平凡改动使用聚焦分支，例如 `feat/clipboard-import`、`fix/term-validation`；
- 提交信息沿用 `feat: add clipboard import preview` 风格；
- 推荐类型：`feat`、`fix`、`docs`、`test`、`refactor`、`ci`、`chore`、`polish`。

## Issue 与 Pull Request

除简单拼写或链接修正外，建议先创建 Issue。PR 应说明用户流程、影响范围和验收标准，并关联对应 Issue。

提交 PR 前请确认：

- 已运行构建与测试，或说明未运行原因；
- `git diff --check` 通过；
- 行为变化已同步更新 README、用户指南或开发文档；
- 未提交 `.db` 档案、教务成绩文本、个人路径或其他敏感信息；
- 涉及界面的改动附上截图或简短说明。
