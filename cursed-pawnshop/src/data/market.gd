class_name MarketData
## Daily metal prices, the display shelf, buyers and the night wholesaler.

## Multiplier range for the price of gold / silver / bronze. Fake metals don't move.
const PRICE_RANGE := Vector2(0.6, 1.8)
## Random daily drift: each day the multiplier moves by up to this much (either way).
const DAILY_DRIFT := 0.15
## Pull back toward 1.0 each day, so prices don't wander off for good.
const MEAN_REVERT := 0.15

## Buyers per day, at most (never more than the items on the shelf).
const BUYERS_PER_DAY := 4
## New purchases get this price tag: market value * this, rounded to 5.
const DEFAULT_TAG := 1.8
## Wholesaler pays market value * this at night.
const WHOLESALE := 0.7

## Personality -> buyer behavior. budget: most they pay, as a multiple of what they
## believe the item is worth (claimed material at today's price). first: opening offer
## as a fraction of their budget. detect: chance they spot a fake before paying.
const BUYERS := {
	"honest": {"budget": 1.6, "first": 0.7, "detect": 0.1, "patience": 4},
	"desperate": {"budget": 2.0, "first": 0.8, "detect": 0.0, "patience": 2},
	"haggler": {"budget": 1.45, "first": 0.55, "detect": 0.6, "patience": 6},
	"liar": {"budget": 1.35, "first": 0.5, "detect": 0.3, "patience": 4},
}
## Asking more than budget * this is an insult (double patience loss).
const BUY_INSULT_RATIO := 1.6
## Selling a fake above its real market value * this is a scam: a sharp buyer may
## catch it on the spot, and a fooled one finds out later (hidden grudge).
## A fake sold near its real worth is honest trade and angers no one.
const SCAM_RATIO := 2.0
const GRUDGE_SOLD_FAKE := 2
const GRUDGE_CAUGHT_FAKE := 2
