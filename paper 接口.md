# paper 接口

## 步骤 1

```markdown
GET https://paper.nsns.in/api/endpoint

返回：
{}

请求头:
Host: paper.nsns.in
Accept-Language: zh-Hans-CN;q=1.0
Accept: */*
Connection: keep-alive
Accept-Encoding: br;q=1.0, gzip;q=0.9, deflate;q=0.8
User-Agent: pap.er/5.3.0 (com.w.paper; build:39; macOS 26.1.0) Alamofire/5.6.4

```

## 步骤 2

```markdown
POST https://paper.nsns.in/graphql

BODY:
{
  "operationName": "UpdateDevice",
  "query": "mutation UpdateDevice($uid: String, $appVer: String, $appBuild: Float, $deviceModel: String, $osName: String, $osVer: String, $lang: String, $prefLang: String, $screens: [String], $distr: Int, $apnsToken: String, $preferences: PreferencesInput) {\n  updateDevice(\n    uid: $uid\n    appVer: $appVer\n    appBuild: $appBuild\n    deviceModel: $deviceModel\n    osName: $osName\n    osVer: $osVer\n    lang: $lang\n    prefLang: $prefLang\n    screens: $screens\n    distr: $distr\n    apnsToken: $apnsToken\n    preferences: $preferences\n  ) {\n    __typename\n    id\n    token\n    nvAvl\n    rs\n  }\n}",
  "variables": {
    "apnsToken": "928059a031961532f0bf5ed23cf61f83116828960efca48e585b129dfc9e926a",
    "appBuild": null,
    "appVer": null,
    "deviceModel": null,
    "distr": null,
    "lang": null,
    "osName": null,
    "osVer": null,
    "preferences": null,
    "prefLang": null,
    "screens": null,
    "uid": "a091dbd6d37cf680ec30955f29f64df9"
  }
}

mac上面 uid 就是system_profiler SPHardwareDataType | awk '/Serial Number/ {print $4}'获取的硬件序列号


返回：
{
  "data": {
    "updateDevice": {
      "__typename": "Device",
      "id": "2564433704491417600",
      "nvAvl": null,
      "rs": null,
      "token": null
    }
  }
}

请求头:
Host: paper.nsns.in
Accept: */*
apollographql-client-version: 5.3.0-39
client-version: 39.0
locale: zh-Hans
Accept-Language: zh-Hans,zh-Hans-CN;q=0.9
Accept-Encoding:gzip, deflate, br
Content-Type: application/json
Content-Length: 934
did: a091dbd6d37cf680ec30955f29f64df9
X-APOLLO-OPERATION-TYPE: mutation
apollographql-client-name: com.w.paper.apollo-ios
User-Agent: pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0
Connection: keep-alive
X-APOLLO-OPERATION-NAME: UpdateDevice

请求头里面的did就是上面的uid

```
```markdown
Linux 获取唯一标志
cat /etc/machine-id

Windows 获取唯一标志
# 1. 获取 CPU ID 并清理前后空白
$cpuId = (wmic cpu get processorid).Trim()

# 2. 获取主板序列号并清理前后空白
$mbSerial = (wmic bios get serialnumber).Trim()

# 3. 将两者拼接成一个字符串
$fingerprint = "$cpuId$mbSerial"

Write-Host "Combined Fingerprint String: $fingerprint"

# 4. 计算该字符串的 MD5 哈希值
# .NET 提供了计算哈希的类，我们直接调用
$stringBytes = [System.Text.Encoding]::UTF8.GetBytes($fingerprint)
$md5 = [System.Security.Cryptography.MD5]::Create()$hashBytes = $md5.ComputeHash($stringBytes)

# 5. 将哈希字节数组转换为十六进制字符串
$uid = -join ($hashBytes | ForEach-Object { "{0:x2}" -f $_ })

Write-Host "Generated Windows UID: $uid"


```

**步骤 3**

```markdown
POST https://paper.nsns.in/graphql

BODY:
{
  "operationName": "CheckinDevice",
  "query": "mutation CheckinDevice($uid: String, $appVer: String, $appBuild: Float, $deviceModel: String, $osName: String, $osVer: String, $lang: String, $prefLang: String, $screens: [String], $distr: Int, $apnsToken: String, $preferences: PreferencesInput) {\n  checkinDevice(\n    uid: $uid\n    appVer: $appVer\n    appBuild: $appBuild\n    deviceModel: $deviceModel\n    osName: $osName\n    osVer: $osVer\n    lang: $lang\n    prefLang: $prefLang\n    screens: $screens\n    distr: $distr\n    apnsToken: $apnsToken\n    preferences: $preferences\n  ) {\n    __typename\n    id\n    token\n    nvAvl\n    rs\n  }\n}",
  "variables": {
    "apnsToken": "928059a031961532f0bf5ed23cf61f83116828960efca48e585b129dfc9e926a",
    "appBuild": 39,
    "appVer": "5.3.0",
    "deviceModel": "Mac16,13",
    "distr": 2,
    "lang": "zh",
    "osName": "OS X",
    "osVer": "26.1.0",
    "preferences": {
      "enableOnMacbookScreenOnly": null,
      "language": "zh-Hans",
      "launchAtLogin": true,
      "localImagesEnabled": true,
      "localStorage": "/Users/krp/Pictures/pap.er",
      "makeTheNotchDisappear": false,
      "randomWallpaper": false,
      "randomWallpaperFrequency": 1,
      "randomWallpaperFromMyLibrary": false,
      "setWallpaperForScreens": false,
      "showIconInDock": false,
      "showStickyRandomWallpapers": true,
      "updateAuto": true
    },
    "prefLang": "zh-Hans",
    "screens": [
      "0.0,0.0,1710.0,1107.0",
      "1710.0,-333.0,2560.0,1440.0"
    ],
    "uid": "a091dbd6d37cf680ec30955f29f64df9"
  }
}

返回：
{
  "data": {
    "checkinDevice": {
      "__typename": "Device",
      "id": "2564433704491417600",
      "nvAvl": false,
      "rs": null,
      "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkaWQiOjI1NjQ0MzM3MDQ0OTE0MTc2MDAsImlhdCI6MTc2NTc2NjA4NiwianRpIjoiMzIwazVwamwzMHJzMWU5djdrMHVlbDkxIn0.iu3PsPIHpc3580QeVBtMfNkifCL4OsAeS7AkpbY5KkE"
    }
  }
}

请求头:
Host: paper.nsns.in
Accept: */*
apollographql-client-version: 5.3.0-39
client-version: 39.0
locale: zh-Hans
Accept-Language: zh-Hans,zh-Hans-CN;q=0.9
Accept-Encoding:gzip, deflate, br
Content-Type: application/json
Content-Length: 1375
did: a091dbd6d37cf680ec30955f29f64df9
X-APOLLO-OPERATION-TYPE: mutation
apollographql-client-name: com.w.paper.apollo-ios
User-Agent: pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0
Connection: keep-alive
X-APOLLO-OPERATION-NAME: CheckinDevice

请求头里面的did就是上面的uid

```

## 步骤4

```markdown
POST https://paper.nsns.in/graphql

BODY:
{
  "operationName": "Lists",
  "query": "query Lists {\n  lists {\n    __typename\n    id\n    name\n    type\n    link\n    position\n    description\n  }\n}",
  "variables": null
}

返回：
{
  "data": {
    "lists": [
      {
        "__typename": "SimpleList",
        "description": null,
        "id": "2244936390884196352",
        "link": null,
        "name": "发现",
        "position": 0,
        "type": "photos"
      },
      {
        "__typename": "SimpleList",
        "description": null,
        "id": "2416408299759992832",
        "link": null,
        "name": "最新",
        "position": 1,
        "type": "photos"
      },
      {
        "__typename": "SimpleList",
        "description": null,
        "id": "2245081321414066176",
        "link": null,
        "name": "竖屏",
        "position": 2,
        "type": "photos"
      }
    ]
  }
}

请求头:
Host: paper.nsns.in
Accept: */*
apollographql-client-version: 5.3.0-39
client-version: 39.0
locale: zh-Hans
Accept-Language: zh-Hans,zh-Hans-CN;q=0.9
Accept-Encoding:gzip, deflate, br
Content-Type: application/json
Content-Length: 170
did: a091dbd6d37cf680ec30955f29f64df9
X-APOLLO-OPERATION-TYPE: query
apollographql-client-name: com.w.paper.apollo-ios
User-Agent: pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0
Connection: keep-alive
X-APOLLO-OPERATION-NAME: Lists

请求头里面的did就是上面的uid

```

## 步骤5

```markdown
POST https://paper.nsns.in/graphql

BODY:
{
  "operationName": "Photos",
  "query": "query Photos($after: String, $before: String, $listId: ID, $filters: PhotosFiltersInput) {\n  photos(after: $after, before: $before, listId: $listId, filters: $filters) {\n    __typename\n    after\n    before\n    listId\n    entries {\n      __typename\n      id\n      type\n      color\n      blurHash\n      creator\n      urls {\n        __typename\n        thumb\n      }\n      width\n      height\n      link\n      linkable\n      heading\n    }\n  }\n}",
  "variables": {
    "after": null,
    "before": null,
    "filters": {},
    "listId": "2244936390884196352"
  }
}

返回：
就是 100 个数据


请求头:
Host: paper.nsns.in
Accept: */*
apollographql-client-version: 5.3.0-39
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkaWQiOjI1NjQ0MzM3MDQ0OTE0MTc2MDAsImxhdCI6MTc2NTc2NjA0NiwiaWF0IjoxNzA2MTY2OTM2LCJ1c2VyX2lkIjoiNTgwQeVBtMfNkifCL4OsAeS7AkpbY5KkE
client-version: 39.0
locale: zh-Hans
Accept-Language: zh-Hans,zh-Hans-CN;q=0.9
Accept-Encoding:gzip, deflate, br
Content-Type: application/json
Content-Length: 585
did: a091dbd6d37cf680ec30955f29f64df9
X-APOLLO-OPERATION-TYPE: query
apollographql-client-name: com.w.paper.apollo-ios
User-Agent: pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0
Connection: keep-alive
X-APOLLO-OPERATION-NAME: Photos

请求头里面的did就是上面的uid
```

![image.png](https://alidocs.oss-cn-zhangjiakou.aliyuncs.com/res/jP2lRYjwMY6zZO8g/img/04597a12-3e94-4464-aafb-d3082bb298af.png)

## 步骤6

```markdown
POST https://paper.nsns.in/graphql


BODY:
{
  "operationName": "UpdateDevice",
  "query": "mutation UpdateDevice($uid: String, $appVer: String, $appBuild: Float, $deviceModel: String, $osName: String, $osVer: String, $lang: String, $prefLang: String, $screens: [String], $distr: Int, $apnsToken: String, $preferences: PreferencesInput) {\n  updateDevice(\n    uid: $uid\n    appVer: $appVer\n    appBuild: $appBuild\n    deviceModel: $deviceModel\n    osName: $osName\n    osVer: $osVer\n    lang: $lang\n    prefLang: $prefLang\n    screens: $screens\n    distr: $distr\n    apnsToken: $apnsToken\n    preferences: $preferences\n  ) {\n    __typename\n    id\n    token\n    nvAvl\n    rs\n  }\n}",
  "variables": {
    "apnsToken": null,
    "appBuild": null,
    "appVer": null,
    "deviceModel": null,
    "distr": null,
    "lang": null,
    "osName": null,
    "osVer": null,
    "preferences": {
      "enableOnMacbookScreenOnly": null,
      "language": "zh-Hans",
      "launchAtLogin": true,
      "localImagesEnabled": true,
      "localStorage": "/Users/krp/Pictures/pap.er",
      "makeTheNotchDisappear": false,
      "randomWallpaper": false,
      "randomWallpaperFrequency": 1,
      "randomWallpaperFromMyLibrary": false,
      "setWallpaperForScreens": false,
      "showIconInDock": true,
      "showStickyRandomWallpapers": true,
      "updateAuto": true
    },
    "prefLang": null,
    "screens": null,
    "uid": "a091dbd6d37cf680ec30955f29f64df9"
  }
}

返回：
{
  "data": {
    "updateDevice": {
      "__typename": "Device",
      "id": "2564433704491417600",
      "nvAvl": null,
      "rs": null,
      "token": null
    }
  }
}



请求头:
Host: paper.nsns.in
Accept: */*
apollographql-client-version: 5.3.0-39
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkaWQiOjI1NjQ0MzM3MDQ0OTE0MTc2MDAsImxhdCI6MTc2NTc2NjA0NiwiaWF0IjoxNzA2MTY2OTM2LCJ1c2VyX2lkIjoiNTgwQeVBtMfNkifCL4OsAeS7AkpbY5KkE
client-version: 39.0
locale: zh-Hans
Accept-Language: zh-Hans,zh-Hans-CN;q=0.9
Accept-Encoding:gzip, deflate, br
Content-Type: application/json
Content-Length: 1243
did: a091dbd6d37cf680ec30955f29f64df9
X-APOLLO-OPERATION-TYPE: mutation
apollographql-client-name: com.w.paper.apollo-ios
User-Agent: pap.er/39 CFNetwork/3860.200.71 Darwin/25.1.0
Connection: keep-alive
X-APOLLO-OPERATION-NAME: UpdateDevice

请求头里面的did就是上面的uid
```