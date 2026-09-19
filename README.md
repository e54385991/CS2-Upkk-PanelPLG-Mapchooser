# CS2-Upkk-PanelPLG-Mapchooser

[![Release](https://img.shields.io/github/v/release/e54385991/CS2-Upkk-PanelPLG-Mapchooser?label=release)](https://github.com/e54385991/CS2-Upkk-PanelPLG-Mapchooser/releases)

用于管理 CS2 服务器的 `MapChooser` 地图池，并快速添加、更新或切换 Steam 创意工坊地图。

当前源码版本：`2.5.3`

> **说明**
>
> 地图池管理以及快速添加 Steam 创意工坊地图功能，**仅插件中心的 [CS2-Upkk-PanelPLG-Mapchooser](http://192.168.50.245:3000/plugins/37) 支持**。其他来源、旧版本或第三方改版不保证包含这些能力。

## 主要功能

- 提供 RTV、地图提名、地图投票、延长地图和自动换图。
- 支持通过 `maps.txt` 管理服务器地图池。
- 支持快速添加或更新 Steam 创意工坊地图，并请求服务器下载。
- 支持通过地图名、Workshop ID 或 `ws:Workshop ID` 立即换图。
- 支持本地 VPK 地图和 Steam 创意工坊地图混合使用。
- 提供中文、英文和拉脱维亚语文本。

## 安装

1. 从 [Releases](https://github.com/e54385991/CS2-Upkk-PanelPLG-Mapchooser/releases) 下载最新的 `MapChooser-vX.Y.Z.zip`。
2. 将压缩包解压到 CS2 服务器的 `game/csgo` 目录。
3. 确认插件与配置文件位于以下路径：

```text
game/csgo/addons/counterstrikesharp/plugins/MapChooser/
game/csgo/addons/counterstrikesharp/configs/plugins/MapChooser/
```

4. 服务器需使用 CounterStrikeSharp API `255` 或更高版本。
5. 自动下载创意工坊地图需要服务器支持 `mm_download_addon` 命令。

## 文件位置

```text
csgo/addons/counterstrikesharp/configs/plugins/MapChooser/config.json
csgo/addons/counterstrikesharp/configs/plugins/MapChooser/maps.txt
csgo/addons/counterstrikesharp/configs/plugins/MapChooser/map_history.txt
csgo/addons/counterstrikesharp/configs/plugins/MapChooser/MapPing.json
```

`maps.txt` 是地图池文件。通过管理命令新增或更新地图后，插件会自动重载地图池；手动编辑 `maps.txt` 后请执行 `css_reload_maplist`。

## 地图池管理

以下命令需要 `@css/ban` 权限：

| 命令 | 说明 |
| --- | --- |
| `css_mce_add_wsmap <地图名> <Workshop ID>` | 添加工坊地图；地图已存在时更新其 Workshop ID，并执行 `mm_download_addon <Workshop ID>`。 |
| `css_mce_add_localmap <地图名[.vpk]>` | 将服务器 `csgo/maps` 中已安装的本地 VPK 地图加入地图池，Workshop ID 记为 `0`。 |
| `css_mce_wsmap <地图名\|Workshop ID\|ws:Workshop ID>` | 立即切换到指定地图。地图名命中 `maps.txt` 且存在有效 Workshop ID 时执行 `host_workshop_map`。 |
| `css_mce_download_maplist` | 批量下载或更新地图池中关联的创意工坊地图。 |
| `css_update_wsmapid <Workshop ID>` | 按 Workshop ID 查找地图并重新请求服务器下载。 |
| `css_update_ws_collection` | 更新创意工坊地图订阅。 |
| `css_reload_maplist` | 重新读取 `maps.txt`，并清空当前地图提名。 |
| `css_setnextmap <地图名>` | 将指定地图设置为下一张地图。 |
| `css_setnextmapwsid <Workshop ID>` | 通过 Workshop ID 设置下一张地图。 |
| `css_random_map` | 随机切换地图。 |

示例：

```text
css_mce_add_wsmap workshop_de_dust2 1234567890
css_mce_add_localmap de_custom
css_mce_wsmap workshop_de_dust2
css_mce_wsmap 1234567890
css_mce_wsmap ws:1234567890
```

地图名建议使用服务器实际的地图文件名，不包含 `.vpk`，且只能包含英文字母、数字、下划线、短横线和点。

### `css_mce_wsmap` 解析顺序

1. 输入为有效数字或 `ws:<数字>` 时，执行 `host_workshop_map <Workshop ID>`。
2. 输入为地图名时，先根据 `Name`、`filename`、`updatedname`、`search` 字段匹配 `maps.txt`。
3. 匹配到有效 Workshop ID 后，执行 `host_workshop_map <Workshop ID>`。
4. 匹配到本地地图但没有有效 Workshop ID 时，使用 MapChooser 本地换图流程。
5. 地图名未匹配到 `maps.txt` 时，回退到 `ds_workshop_changelevel <地图名>`。

## 玩家命令

| 游戏内命令 | 说明 |
| --- | --- |
| `!rtv` | 发起或参与 RTV 换图投票。 |
| `!unrtv` | 取消自己的 RTV。 |
| `!nominate [地图名或关键词]` | 提名下一张地图；别名 `!yd`。 |
| `!ydlist` / `!nominatelist` | 查看当前地图提名。 |
| `!nextmap` | 查看下一张地图。 |
| `!timeleft` | 查看当前地图剩余时间。 |
| `!mapinfo` | 查看当前地图信息。 |

## 配置

配置文件位于 `configs/plugins/MapChooser/config.json`，常用配置项如下：

| 配置项 | 默认值 | 说明 |
| --- | ---: | --- |
| `VoteStartTime` | `3.0` | 地图结束前多少分钟开始地图投票。 |
| `VoteDuration` | `15.0` | 地图投票持续时间，单位为秒。 |
| `IncludeMaps` | `5` | 投票菜单中随机选取的地图数量。 |
| `AllowRtv` | `true` | 是否允许玩家使用 RTV。 |
| `RTVPercent` | `0.6` | 发起 RTV 所需的玩家比例。 |
| `ExtendTimeStep` | `10.0` | 每次延长地图的分钟数。 |
| `ExtendLimit` | `3` | 单张地图允许延长的最大次数。 |
| `ChangeMapUse_host_workshop_map` | `true` | 自动换图默认使用 `host_workshop_map`。 |
| `CrashMapRecover` | `false` | 是否启用地图崩溃恢复。 |

## 构建

项目使用 .NET 10 和 CounterStrikeSharp API：

```bash
dotnet restore MapChooser/MapChooser/MapChooser.csproj
dotnet build MapChooser/MapChooser/MapChooser.csproj --configuration Release
dotnet publish MapChooser/MapChooser/MapChooser.csproj --configuration Release
```

推送 `v*` 标签后，GitHub Actions 会自动构建并创建 GitHub Release。
