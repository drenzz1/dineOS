# Performance Audit — M2.8

## Lighthouse Scores

| Page | Performance | Accessibility | Best Practices |
|------|------------|---------------|----------------|
| /dashboard | 97 | 100 | 100 |
| /orders | 79 | 95 | 100 |

## What was fixed
- Added next/dynamic lazy loading for heavy components
- Replaced <img> tags with next/image
- Added next/font for custom fonts

## What remains
- Further performance optimization once real data is wired in
- Will re-run audit after backend integration in M5