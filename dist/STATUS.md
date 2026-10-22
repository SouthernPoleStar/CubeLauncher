# 服务状态

最后更新：2026-10-22

## 模组下载

**来源：Modrinth / CurseForge 双源**

下载中心右上角可切换两个模组源，选择会写入 `config.json` 的 `ModSource`。

Modrinth：

- 直接使用 Modrinth 官方 API；
- 模组文件直接从 `cdn.modrinth.com` 下载；
- 必需前置模组自动安装；
- 名称/简介优先从 MC百科（mcmod.cn）搜索页读取中文，带内存缓存；无匹配或网络失败时保留英文；
- WebP 封面通过 Windows WIC 解码并缓存。

CurseForge：

- 使用官方 Core API（`api.curseforge.com`），按游戏版本与加载器过滤；
- 支持模组、材质包、光影搜索与安装，必需前置自动安装；
- 文件优先使用 API 返回的下载地址，作者关闭第三方分发时回退 `edge.forgecdn.net`；
- API Key 内置，可用环境变量 `CURSEFORGE_API_KEY` 覆盖；
- 整合包暂不支持一键导入，仅 Modrinth `.mrpack`。

## 游戏下载与 Java

| 服务 | 状态 |
| --- | --- |
| Mojang 版本清单 / 客户端 | 可用（BMCLAPI 优先） |
| 依赖库 / 资源文件 | 可用（BMCLAPI 优先） |
| Forge 安装器 / Maven | 可用（BMCLAPI 优先） |
| NeoForge 安装器 / 库 | 可用（NeoForged CDN；1.20.1 / 1.20.4 / 1.21.1） |
| Fabric Meta / Loader | 可用（BMCLAPI 优先） |
| Java（Temurin） | 可用（清华 TUNA 优先） |
| 微软登录 | 可用（设备代码流程） |
| LittleSkin / authlib-injector | 可用 |

## 字体

字体已恢复为系统字体：正文和标题使用微软雅黑 UI（回退 Microsoft YaHei / Segoe UI），不再内嵌自定义字体。

## 实例与整合包

- 首页点击实例会进入独立的「选择游戏」页面：左侧 Minecraft 版本，右侧加载器实例，带 Forge/Fabric/Quilt/NeoForge/原版标志；同名实例追加中文序号。
- 实例导出为 Modrinth `.mrpack`（`modrinth.index.json` + `overrides/`），可直接重新导入。
- 游戏运行时首页提供“关闭游戏”：先请求正常退出，超时后强制结束 JVM。
