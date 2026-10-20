# 服务状态

最后更新：2026-10-20

## CurseForge 下载

**状态：不可用**

原因：

- MCIM 国内镜像当前返回 `503 Service Unavailable`；
- 启动器未内置 CurseForge 官方 API Key（官方条款禁止共享/内置 Key）；
- 无 API Key 时，CurseForge 官方 API 返回 `403 Forbidden`。

当前回退方案：

- 启动器会尝试通过 CFWidget 按项目 slug 获取信息与 ForgeCDN 下载地址；
- 只支持部分项目（例如 `jei`、`sodium`、`create`、`fabric-api`）；
- CFWidget 不返回前置依赖，可能需要手动安装；
- 完整搜索、分页和依赖解析不可用。

## Modrinth

**状态：可用**

- MCIM 可用时优先走国内镜像；
- MCIM 不可用时自动回退 Modrinth 官方 API；
- 模组文件优先走 MCIM，失败后回退 `cdn.modrinth.com`。

## 游戏下载与 Java

| 服务 | 状态 |
| --- | --- |
| Mojang 版本清单 / 客户端 | 可用（BMCLAPI 优先） |
| 依赖库 / 资源文件 | 可用（BMCLAPI 优先） |
| Forge 安装器 / Maven | 可用（BMCLAPI 优先） |
| NeoForge 安装器 / 库 | 可用（1.20.4 / 1.21.1；20.4 自动修补 JarJar 镜像） |
| Fabric Meta / Loader | 可用（BMCLAPI 优先） |
| Java（Temurin） | 可用（清华 TUNA 优先） |
| 微软登录 | 可用（设备代码流程） |
| LittleSkin / authlib-injector | 可用 |

## 恢复 CurseForge

任选其一：

1. 等待 MCIM 镜像恢复；
2. 自行申请 CurseForge API Key，并在「设置 → 下载与镜像 → CurseForge API Key」中填写；
3. 改用 Modrinth 源。

> API Key 仅保存在本机配置中，请勿提交到 GitHub 或分享给他人。
