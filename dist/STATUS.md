# 服务状态

最后更新：2026-10-20

## 模组下载

**来源：仅 Modrinth**

CurseForge 下载通道已彻底移除，启动器不再请求 `api.curseforge.com`、CFWidget 或 ForgeCDN。

Modrinth：

- MCIM 可用时优先走国内镜像；
- MCIM 不可用时自动回退 Modrinth 官方 API；
- 模组文件优先走 MCIM，失败后回退 `cdn.modrinth.com`；
- 必需前置模组自动安装；
- 名称/简介通过 MyMemory 翻译为中文，带内存缓存；翻译失败时保留英文；
- WebP 封面通过 Windows WIC 解码并缓存。

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
