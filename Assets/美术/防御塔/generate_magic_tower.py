from PIL import Image, ImageDraw, ImageFilter, ImageFont
import math

def draw_brick_rect(draw, x, y, width, height, colors):
    # 主要填充
    draw.rectangle([x, y, x + width, y + height], fill=colors['base'])
    
    # 阴影
    draw.rectangle([x, y, x + width/2, y + height], fill=colors['shadow'])
    
    # 高光
    draw.rectangle([x + width/2, y, x + width*3/4, y + height], fill=colors['highlight'])
    
    # 砖缝
    brick_color = '#3a3a5a'
    for i in range(0, height, 20):
        draw.line([x, y + i, x + width, y + i], fill=brick_color, width=2)
    
    # 垂直砖缝（错开排列）
    for row in range(0, height, 20):
        offset = 20 if (row // 20) % 2 == 0 else 0
        for i in range(offset, width, 40):
            draw.line([x + i, y + row, x + i, y + row + 20], fill=brick_color, width=2)
    
    # 轮廓
    draw.rectangle([x, y, x + width, y + height], outline='#2a2a4a', width=3)

def draw_magic_roof(draw, x, y, width):
    # 创建渐变
    points = [(x, y), (x + width, y), (x + width//2, y - 40)
    
    # 简单的三角形
    draw.polygon(points, fill='#7a4ada', outline='#3a2a8a', width=3)
    
    # 添加渐变效果（通过线条模拟
    for i in range(0, 40, 4):
        color = f'#{min(0x5a + i, 0x9a):02x}{min(0x3a + i//2, 0x6a):02x}{min(0xba + i//2, 0xff):02x}'
        t = i / 40
        px1 = x + width * t
        px2 = x + width * (1-t)
        py = y - i
        draw.line([px1, py, px2, py], fill=color, width=4)

def draw_magic_crystal(draw, x, y):
    # 光晕
    for i in range(35, 0, -5):
        alpha = int(255 - i * 7)
        color = (180, 120, 255, alpha)
        draw.ellipse([x - i, y - i, x + i, y + i], fill=color)
    
    # 水晶主体
    crystal_points = [
        (x, y - 25),
        (x + 15, y),
        (x + 10, y + 20),
        (x - 10, y + 20),
        (x - 15, y)
    ]
    draw.polygon(crystal_points, fill='#a060ff', outline='#6030cc', width=2)
    
    # 高光
    draw.line([x - 5, y - 15, x, y - 5, x - 8, y + 5], fill='white', width=3)

def draw_star(draw, x, y, size, color):
    points = []
    for i in range(5):
        angle = (i * 4 * math.pi / 5) - math.pi / 2
        px = x + math.cos(angle) * size
        py = y + math.sin(angle) * size
        points.append((px, py))
    draw.polygon(points, fill=color)

def draw_window(draw, x, y, width, height):
    # 窗框
    draw.rectangle([x - 2, y - 2, x + width + 2, y + height + 2], fill='#2a2a4a')
    
    # 发光效果
    for i in range(height, 0, -2):
        alpha = int(255 - i * 10)
        color = (150, 200, 255, max(0, alpha))
        draw.line([x, y + height - i, x + width, y + height - i], fill=color, width=2)

def draw_magic_runes(draw, x, y):
    # 简单的魔法符文
    rune_color = '#aa88ff'
    draw.ellipse([x - 30 - 8, y - 8, x - 30 + 8, y + 8], outline=rune_color, width=2)
    draw.ellipse([x + 30 - 8, y - 8, x + 30 + 8, y + 8], outline=rune_color, width=2)
    draw.line([x, y - 10, x, y + 10], fill=rune_color, width=2)
    draw.line([x - 8, y, x + 8, y], fill=rune_color, width=2)

def generate_magic_tower():
    # 创建透明背景
    size = 512
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    
    # 中心点和缩放
    center_x, center_y = size // 2, size - 100
    scale = 1.5
    
    # 塔楼颜色
    tower_colors = {
        'shadow': '#4a4a6a',
        'base': '#6a6a9a',
        'highlight': '#9a9aca',
        'top': '#7a4ada'
    }
    
    # 画塔楼各部分
    draw_brick_rect(draw, center_x - 60*scale, center_y, 120*scale, 40*scale, tower_colors)
    draw_brick_rect(draw, center_x - 50*scale, center_y - 50*scale, 100*scale, 50*scale, tower_colors)
    draw_brick_rect(draw, center_x - 40*scale, center_y - 100*scale, 80*scale, 50*scale, tower_colors)
    draw_brick_rect(draw, center_x - 45*scale, center_y - 110*scale, 90*scale, 10*scale, tower_colors)
    
    # 魔法塔顶
    draw_magic_roof(draw, center_x - 45*scale, center_y - 110*scale, 90*scale)
    
    # 魔法水晶
    draw_magic_crystal(draw, center_x, center_y - 150*scale)
    
    # 魔法光环
    for i in range(3):
        r = (50 - i * 10) * scale
        alpha = int((0.3 - i * 0.1) * 255
        color = (150, 100, 255, int(alpha))
        draw.ellipse([center_x - r, center_y - 130*scale - r, center_x + r, center_y - 130*scale + r], outline=color, width=3)
    
    # 魔法星星
    star_positions = [(-30, -20), (25, -15), (-20, 25), (30, 20), (0, -40)]
    for i, (sx, sy) in enumerate(star_positions):
        draw_star(draw, center_x + sx*scale, center_y - 130*scale + sy*scale, (5 + (i % 2) * 3 * scale, '#ffdd44')
    
    # 窗户
    draw_window(draw, center_x - 20*scale, center_y - 70*scale, 15*scale, 20*scale)
    draw_window(draw, center_x + 5*scale, center_y - 70*scale, 15*scale, 20*scale)
    draw_window(draw, center_x - 10*scale, center_y - 30*scale, 15*scale, 20*scale)
    
    # 魔法符文
    draw_magic_runes(draw, center_x, center_y - 20*scale)
    
    return img

if __name__ == '__main__':
    tower_image = generate_magic_tower()
    tower_image.save('Assets/Art/Towers/magic_tower.png')
    print('✅ 魔法塔图片已生成: Assets/Art/Towers/magic_tower.png')
    
    # 同时生成一个更简单的像素风版本
    simple_img = generate_magic_tower()
    # 可以在这里添加像素化处理
    simple_img.save('Assets/Art/Towers/magic_tower_simple.png')
    print('✅ 简单版魔法塔图片已生成: Assets/Art/Towers/magic_tower_simple.png')
