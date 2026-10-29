class_name UIFont
## Galmuri pixel fonts (OFL, assets/fonts) at their native pixel sizes. A bitmap
## font only stays crisp at its design size or an integer multiple, so every text
## size in the game goes through px() / font() instead of an arbitrary number.

const SMALL := preload("res://assets/fonts/Galmuri9.ttf")  # 10 px
const BODY := preload("res://assets/fonts/Galmuri11.ttf")  # 12 px
const BOLD := preload("res://assets/fonts/Galmuri11-Bold.ttf")  # 12 px
const BIG := preload("res://assets/fonts/Galmuri14.ttf")  # 15 px (x2 = 30, x3 = 45)


## Snaps a requested size to a crisp one: <=9 -> 10, <=12 -> 12, then multiples of 15.
static func px(size: int) -> int:
	if size <= 9:
		return 10
	if size <= 12:
		return 12
	return maxi(15, int(round(size / 15.0)) * 15)


## The font whose design size matches px(size).
static func font(size: int) -> Font:
	match px(size):
		10:
			return SMALL
		12:
			return BODY
	return BIG


## Sets a Label / Button / CheckButton to the crisp font for [param size].
static func style(c: Control, size: int) -> void:
	c.add_theme_font_override("font", font(size))
	c.add_theme_font_size_override("font_size", px(size))
