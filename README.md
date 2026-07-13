# BUPT GPA Calculator

BUPT GPA Calculator 是一个面向北京邮电大学学生的本地 Windows 桌面 GPA 计算器。它使用单个 SQLite 成绩档案保存多个学生的数据，支持手动录入、从教务系统成绩表复制导入、按学期筛选统计，以及完整展示 GPA 计算规则。

> 项目仍处于早期开发阶段，当前尚未发布可用版本。

## 设计原则

- **本地优先**：不登录教务系统，不保存账号、密码、Cookie，也不上传成绩数据。
- **档案即数据**：用户自行新建或打开 `.db` 成绩档案；软件不在系统目录留下运行时数据。
- **结果可核对**：固定复刻原始程序的百分制 GPA 对照表，并提供逐课计算明细。
- **轻量可携带**：最终发布为无需安装的 Windows x64 单文件程序。

## 计划功能

- 以学号切换多个学生；
- 录入课程名称、课程编号、成绩、学分、学期与“是否计入计算”；
- 从北邮教务系统课程成绩结果表复制并导入；
- 计算 GA、GPA、总学分和课程数，支持生涯、单学期和多学期范围；
- 展示学期 GPA 趋势、成绩分布、完整换算规则与逐课计算明细；
- 保存、打开和另存为单文件 SQLite 成绩档案。

## 使用与开发

- 面向普通用户的操作说明见 [docs/user-guide.md](docs/user-guide.md)。
- 本地开发、测试与发布命令见 [docs/development.md](docs/development.md)。
- 贡献流程见 [CONTRIBUTING.md](CONTRIBUTING.md)。
- 重要更新见 [CHANGELOG.md](CHANGELOG.md)。

## 隐私

请不要将个人 `.db` 成绩档案、教务成绩复制内容或包含本机绝对路径的截图提交到 GitHub。详细说明见 [SECURITY.md](SECURITY.md)。

## 许可证

本项目采用 [MIT License](LICENSE)。

小Z工作室#2026
