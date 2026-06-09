
from PIL import Image, ImageDraw, ImageColor
import math

# 创建画布，尺寸与兵营塔类似
width, height = 512, 512
image = Image.new('RGBA', (width, height), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)

center_x = width // 2
center_y = height // 2

# 颜色定义 - 参考兵营塔风格
STONE_GRAY = '#8b8b8b'
STONE_DARK = '#6b6b6b'
STONE_LIGHT = '#9b9b9b'
MAGIC_PURPLE = '#6c5ce7'
MAGIC_DARK = '#5a4ac7'
MAGIC_LIGHT = '#a29bfe'
CRYSTAL = '#a29bfe'
GOLD_RUNE = '#ffd700'
OUTLINE = '#2d2d44'

# 塔基 - 与兵营塔类似的灰色石砖
# 正面
draw.polygon([
    (center_x - 80, center_y + 60),
    (center_x + 80, center_y + 60),
    (center_x + 60, center_y - 20),
    (center_x - 60, center_y - 20)
], fill=STONE_GRAY)

# 右侧阴影
draw.polygon([
    (center_x + 80, center_y + 60),
    (center_x + 60, center_y - 20),
    (center_x + 80, center_y - 40)
], fill=STONE_DARK)

# 左侧高光
draw.polygon([
    (center_x - 80, center_y + 60),
    (center_x - 60, center_y - 20),
    (center_x - 80, center_y - 40)
], fill=STONE_LIGHT)

# 塔身 - 魔法紫灰色
draw.polygon([
    (center_x - 60, center_y - 20),
    (center_x + 60, center_y - 20),
    (center_x + 45, center_y - 120),
    (center_x - 45, center_y - 120)
], fill='#4a3b6b')

# 塔身右侧阴影
draw.polygon([
    (center_x + 60, center_y - 20),
    (center_x + 45, center_y - 120),
    (center_x + 60, center_y - 130),
    (center_x + 80, center_y - 40)
], fill='#3a2b5b')

# 塔身左侧高光
draw.polygon([
    (center_x - 60, center_y - 20),
    (center_x - 45, center_y - 120),
    (center_x - 60, center_y - 130),
    (center_x - 80, center_y - 40)
], fill='#5a4b7b')

# 塔顶 - 魔法蓝紫色金字塔
draw.polygon([
    (center_x - 50, center_y - 120),
    (center_x, center_y - 200),
    (center_x + 50, center_y - 120)
], fill=MAGIC_PURPLE)

# 塔顶右侧阴影
draw.polygon([
    (center_x + 50, center_y - 120),
    (center_x, center_y - 200),
    (center_x + 60, center_y - 130)
], fill=MAGIC_DARK)

# 魔法水晶
draw.polygon([
    (center_x, center_y - 200),
    (center_x - 15, center_y - 185),
    (center_x - 10, center_y - 170),
    (center_x + 10, center_y - 170),
    (center_x + 15, center_y - 185)
], fill=CRYSTAL)

# 水晶发光效果 - 使用半透明圆
for i in range(5):
    alpha = int(255 * (1 - i/5) * 0.6)
    glow_color = (162, 155, 254, alpha)
    radius = 40 - i * 5
    for angle in range(0, 360, 10):
        rad = math.radians(angle)
        x = center_x + math.cos(rad) * radius
        y = center_y - 185 + math.sin(rad) * radius * 0.5
        draw.ellipse([x-2, y-2, x+2, y+2], fill=glow_color)

# 魔法符文
rune_color = GOLD_RUNE
# 使用简单的几何图形模拟符文
# 三个符文点
draw.ellipse([center_x - 28, center_y - 63, center_x - 22, center_y - 57], fill=rune_color)
draw.ellipse([center_x - 3, center_y - 83, center_x + 3, center_y - 77], fill=rune_color)
draw.ellipse([center_x + 22, center_y - 63, center_x + 28, center_y - 57], fill=rune_color)

# 连接符文的线条
draw.line([(center_x - 25, center_y - 60), (center_x, center_y - 80)], fill=rune_color, width=2)
draw.line([(center_x + 25, center_y - 60), (center_x, center_y - 80)], fill=rune_color, width=2)

# 魔法粒子效果
particle_colors = [(162, 155, 254, 255), (253, 121, 168, 255)]
for i in range(8):
    angle = (i / 8) * 2 * math.pi
    radius = 70
    px = center_x + math.cos(angle) * radius
    py = center_y - 100 + math.sin(angle) * radius * 0.5
    color = particle_colors[i % 2]
    draw.ellipse([px-4, py-4, px+4, py+4], fill=color)

# 石砖纹理 - 塔基
for y in range(center_y + 50, center_y - 15, -15):
    draw.line([(center_x - 75, y), (center_x + 75, y)], fill='#5a5a5a', width=2)

for x in range(center_x - 70, center_x + 70, 20):
    draw.line([(x, center_y + 55), (x - 10, center_y - 15)], fill='#5a5a5a', width=2)

# 轮廓线
draw.polygon([
    (center_x - 80, center_y + 60),
    (center_x + 80, center_y + 60),
    (center_x + 60, center_y - 130),
    (center_x, center_y - 200),
    (center_x - 60, center_y - 130)
], outline=OUTLINE, width=3)

# 保存图片
output_path = r'c:\Users\97809\Desktop\sttop5\Assets\Art\Towers\magic_tower.png'
image.save(output_path)
print(f"魔法塔精灵已保存到: {output_path}")

# 显示图片（可选）
image.show()
