# 开发与发布

## 本地开发

```powershell
dotnet restore BuptGpaCalculator.sln
dotnet build BuptGpaCalculator.sln --configuration Release --no-restore
dotnet test BuptGpaCalculator.sln --configuration Release --no-build
```

## 项目结构

- `src/BuptGpaCalculator.App`：基于 WPF-UI 的 Windows 11 风格界面与应用层；
- `src/BuptGpaCalculator.Core`：成绩模型、计算、导入、校验与档案逻辑；
- `tests/BuptGpaCalculator.Tests`：不依赖界面的单元测试。

## 本地发布

```powershell
dotnet restore BuptGpaCalculator.sln --runtime win-x64

dotnet publish src/BuptGpaCalculator.App/BuptGpaCalculator.App.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    --output artifacts/publish
```

发布目录中的 `BUPT-GPA.exe` 是面向用户的 portable 程序。程序不会自动生成成绩档案；用户首次使用时自行选择新建或打开 `.db` 文件。

## 应用图标

发布前将 `.ico` 图标放入 `src/BuptGpaCalculator.App/Assets/AppIcon.ico`，并在 `src/BuptGpaCalculator.App/BuptGpaCalculator.App.csproj` 中设置：

```xml
<ApplicationIcon>Assets\AppIcon.ico</ApplicationIcon>
```

图标文件建议包含 16、24、32、48、64、128、256 像素等常见尺寸，以便任务栏和资源管理器都能清晰显示。当前不在自定义标题栏额外显示图标，避免运行时资源加载影响启动稳定性。

## 版本发布

版本遵循 SemVer。推送 `v0.1.0` 形式的标签后，GitHub Actions 会构建并发布 Windows x64 产物。发布前必须更新 CHANGELOG，并确认 CI 通过。
