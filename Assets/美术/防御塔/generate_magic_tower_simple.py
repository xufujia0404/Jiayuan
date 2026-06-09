from PIL import Image, ImageDraw
import math

def create_magic_tower():
    # 创建512x512透明背景图片
    size = 512
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    center_x = size // 2
    center_y = size - 80
    scale = 1.4
    
    # 塔楼颜色
    shadow_color = (74, 74, 106, 255)
    base_color = (106, 106, 154, 255)
    highlight_color = (154, 154, 202, 255)
    outline_color = (42, 42, 74, 255)
    
    # 画塔楼底部
    x, y = center_x - 60*scale, center_y
    w, h = 120*scale, 40*scale
    draw.rectangle([x, y, x+w, y+h], fill=base_color, outline=outline_color, width=3)
    draw.rectangle([x, y, x+w/2, y+h], fill=shadow_color)
    draw.rectangle([x+w/2, y, x+w*3/4, y+h], fill=highlight_color)
    
    # 塔楼主体1
    x, y = center_x - 50*scale, center_y - 50*scale
    w, h = 100*scale, 50*scale
    draw.rectangle([x, y, x+w, y+h], fill=base_color, outline=outline_color, width=3)
    draw.rectangle([x, y, x+w/2, y+h], fill=shadow_color)
    draw.rectangle([x+w/2, y, x+w*3/4, y+h], fill=highlight_color)
    
    # 塔楼主体2
    x, y = center_x - 40*scale, center_y - 100*scale
    w, h = 80*scale, 50*scale
    draw.rectangle([x, y, x+w, y+h], fill=base_color, outline=outline_color, width=3)
    draw.rectangle([x, y, x+w/2, y+h], fill=shadow_color)
    draw.rectangle([x+w/2, y, x+w*3/4, y+h], fill=highlight_color)
    
    # 塔楼顶部
    x, y = center_x - 45*scale, center_y - 110*scale
    w, h = 90*scale, 10*scale
    draw.rectangle([x, y, x+w, y+h], fill=base_color, outline=outline_color, width=3)
    
    # 魔法塔顶（三角形）
    roof_x1, roof_y1 = center_x - 45*scale, center_y - 110*scale
    roof_x2, roof_y2 = center_x + 45*scale, center_y - 110*scale
    roof_x3, roof_y3 = center_x, center_y - 150*scale
    draw.polygon([(roof_x1, roof_y1), (roof_x2, roof_y2), (roof_x3, roof_y3)], 
                 fill=(122, 74, 218, 255), outline=(58, 42, 138, 255), width=3)
    
    # 魔法水晶
    crystal_x, crystal_y = center_x, center_y - 170*scale
    # 水晶光晕
    for i in range(30, 0, -3):
        alpha = int(255 - i * 8)
        glow_color = (180, 120, 255, min(255, alpha))
        draw.ellipse([crystal_x-i, crystal_y-i, crystal_x+i, crystal_y+i], fill=glow_color)
    
    # 水晶主体
    crystal_points = [
        (crystal_x, crystal_y - 20*scale),
        (crystal_x + 12*scale, crystal_y),
        (crystal_x + 8*scale, crystal_y + 16*scale),
        (crystal_x - 8*scale, crystal_y + 16*scale),
        (crystal_x - 12*scale, crystal_y)
    ]
    draw.polygon(crystal_points, fill=(160, 96, 255, 255), outline=(96, 48, 204, 255), width=2)
    
    # 水晶高光
    draw.line([(crystal_x - 4*scale, crystal_y - 12*scale), 
               (crystal_x, crystal_y - 4*scale), 
               (crystal_x - 6*scale, crystal_y + 4*scale)], 
              fill=(255, 255, 255, 200), width=3)
    
    # 魔法光环
    for i in range(3):
        r = (40 - i * 8) * scale
        alpha = int((0.3 - i * 0.1) * 255)
        glow_color = (150, 100, 255, alpha)
        draw.ellipse([center_x - r, center_y - 145*scale - r, 
                      center_x + r, center_y - 145*scale + r], 
                     outline=glow_color, width=3)
    
    # 魔法星星
    star_positions = [(-25, -15), (20, -12), (-16, 20), (25, 16), (0, -32)]
    star_colors = [(255, 221, 68, 255), (255, 200, 100, 255)]
    for i, (sx, sy) in enumerate(star_positions):
        star_x = center_x + sx * scale
        star_y = center_y - 145*scale + sy * scale
        star_size = (4 + (i % 2) * 2) * scale
        
        star_points = []
        for j in range(5):
            angle = (j * 4 * math.pi / 5) - math.pi / 2
            px = star_x + math.cos(angle) * star_size
            py = star_y + math.sin(angle) * star_size
            star_points.append((px, py))
        draw.polygon(star_points, fill=star_colors[i % 2])
    
    # 窗户
    window_color = (80, 100, 200, 200)
    window_frame = (42, 42, 74, 255)
    
    # 窗户1
    wx, wy = center_x - 18*scale, center_y - 65*scale
    ww, wh = 14*scale, 18*scale
    draw.rectangle([wx-2, wy-2, wx+ww+2, wy+wh+2], fill=window_frame)
    draw.rectangle([wx, wy, wx+ww, wy+wh], fill=window_color)
    
    # 窗户2
    wx, wy = center_x + 4*scale, center_y - 65*scale
    draw.rectangle([wx-2, wy-2, wx+ww+2, wy+wh+2], fill=window_frame)
    draw.rectangle([wx, wy, wx+ww, wy+wh], fill=window_color)
    
    # 窗户3
    wx, wy = center_x - 9*scale, center_y - 25*scale
    draw.rectangle([wx-2, wy-2, wx+ww+2, wy+wh+2], fill=window_frame)
    draw.rectangle([wx, wy, wx+ww, wy+wh], fill=window_color)
    
    # 魔法符文
    rune_color = (170, 136, 255, 255)
    draw.ellipse([center_x - 26*scale - 6, center_y - 16*scale - 6, 
                  center_x - 26*scale + 6, center_y - 16*scale + 6], 
                 outline=rune_color, width=2)
    draw.ellipse([center_x + 26*scale - 6, center_y - 16*scale - 6, 
                  center_x + 26*scale + 6, center_y - 16*scale + 6], 
                 outline=rune_color, width=2)
    draw.line([(center_x, center_y - 24*scale), (center_x, center_y - 8*scale)], 
              fill=rune_color, width=2)
    draw.line([(center_x - 6*scale, center_y - 16*scale), (center_x + 6*scale, center_y - 16*scale)], 
              fill=rune_color, width=2)
    
    return img

if __name__ == '__main__':
    try:
        print('🎨 正在生成魔法塔图片...')
        tower_image = create_magic_tower()
        
        # 保存图片
        output_path = 'Assets/Art/Towers/magic_tower.png'
        tower_image.save(output_path)
        print(f'✅ 魔法塔图片已生成: {output_path}')
        
        # 同时创建一个.meta文件
        meta_content = '''fileFormatVersion: 2
guid: 1234567890abcdef1234567890abcdef
TextureImporter:
  externalObjects: {}
  serializedVersion: 10
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  vtOnly: 0
  ignorePngGamma: 0
  ignoreMasterTextureLimit: 0
  cubeMap:
    convertToCubemap: 0
    cubemapConvolution: 0
    seamlessCubemap: 0
    textureFormat: 6
    mipCount: 0
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
    glossinessReflections: 0
    fixEdgeArtifacts: 0
  textureFormat: -1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: -1
    mipBias: -100
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings: []
  spriteSheet:
    serializedVersion: 2
    tiles: []
    sprites: []
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'''
        with open('Assets/Art/Towers/magic_tower.png.meta', 'w', encoding='utf-8') as f:
            f.write(meta_content)
        print('✅ Meta文件已创建')
        
        print('\n🎉 完成！现在你可以在Unity中看到魔法塔了！')
        
    except Exception as e:
        print(f'❌ 错误: {e}')
        import traceback
        traceback.print_exc()
