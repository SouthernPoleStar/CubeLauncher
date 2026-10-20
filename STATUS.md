# 服务状态

最后更新：2026-10-20

## CurseForge 下载

**状态：部分可用（无 API Key 仅支持精确 slug 查询）**

已确认：

- ForgeCDN 的 `mediafilez.forgecdn.net` 可用（实测 `206`），`edge.forgecdn.net` 对同一文件可能是 `404`；启动器已改为 mediafilez 优先、edge 回退；
- 文件路径不能带随机目录（例如 `AU9c1WP`），否则返回 `403 AccessDenied`；
- CFWidget 项目接口可用，但单次响应约 2.7 MB、耗时可达 15 秒，启动器已做 10 分钟内存缓存；
- CFWidget 不带 `version` 参数会返回最老的文件（JEI 会返回 1.12.2），启动器现在始终带 `version` + `loader`，并按 `versions[]` 同时校验 MC 版本与加载器，按上传时间倒序。

平台限制：

- `api.cfwidget.com` **没有搜索接口**，`/search`、`/v2/search` 均为 404；
- `api.curse.tools` 已关闭（403）；
- `api.curseforge.com` 无 Key 返回 403。

因此：

- 输入已知项目 slug（如 `jei`、`sodium`、`create`）可以搜索、选择文件并下载；
- **不能**像 Modrinth 一样做关键词模糊搜索、分页和依赖解析；
- CFWidget 不返回前置依赖，需要手动安装；
- 填写 CurseForge API Key 后可恢复完整官方 API。

Modrinth 仍完整可用。

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
| NeoForge 安装器 / 库 | 可用（NeoForged CDN；1.20.1 / 1.20.4 / 1.21.1） |
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
