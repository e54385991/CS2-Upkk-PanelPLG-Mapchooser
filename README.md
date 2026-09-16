# CS2-Upkk-PanelPLG-Mapchooser

用于管理 CS2 服务器的 `MapChooser` 地图池，并快速添加或更新 Steam 创意工坊地图。

> **说明**
>
> 地图池管理以及快速添加 Steam 创意工坊地图功能，**仅插件中心的 [CS2-Upkk-PanelPLG-Mapchooser](http://192.168.50.245:3000/plugins/37) 支持**。其他来源、旧版本或第三方改版不保证包含这些能力。

## 地图文件

地图池文件位于：

```text
csgo/addons/counterstrikesharp/configs/plugins/MapChooser/maps.txt
```

通过管理命令新增或更新地图后，插件会自动重载地图池，无需手动编辑 `maps.txt`。

## 管理命令

以下命令需要 `@css/ban` 权限：

- `css_mce_add_wsmap <地图名> <Workshop ID>`：添加 Steam 创意工坊地图；地图已存在时会更新其 Workshop ID，并请求服务器执行 `mm_download_addon`。
- `css_mce_add_localmap <地图名[.vpk]>`：将服务器中已安装的本地 VPK 地图加入地图池，Workshop ID 记为 `0`。
- `css_mce_wsmap <地图名|Workshop ID|ws:Workshop ID>`：立即切换到指定地图；地图存在于 `maps.txt` 时使用 MapChooser 换图流程，否则按输入类型回退到 `host_workshop_map` 或 `ds_workshop_changelevel`。

示例：

```text
css_mce_add_wsmap workshop_de_dust2 1234567890
css_mce_add_localmap de_custom
css_mce_wsmap 1234567890
css_mce_wsmap ws:1234567890
```

地图名建议使用服务器实际的地图文件名（不含 `.vpk`），只能包含英文字母、数字、下划线、短横线和点。

## 构建

项目使用 .NET 10 和 CounterStrikeSharp：

```bash
dotnet restore MapChooser/MapChooser/MapChooser.csproj
dotnet build MapChooser/MapChooser/MapChooser.csproj --configuration Release
```
