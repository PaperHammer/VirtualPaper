"""Compose one README hero from the official icon and two real application captures."""
import base64
from pathlib import Path

HERE = Path(__file__).resolve().parent

def capture(theme):
    return base64.b64encode((HERE / f'library-{theme}-mica.jpg').read_bytes()).decode('ascii')

icon = base64.b64encode((HERE.parents[1] / 'src/VirtualPaper.UI/Assets/icon-vp-512.png').read_bytes()).decode('ascii')

svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="1200" viewBox="0 0 1920 1200" role="img" aria-labelledby="title desc">
  <title id="title">VirtualPaper</title>
  <desc id="desc">The official application icon and product name above two real English application captures with Mica: a larger light-theme library in front of a dark-theme library, both displaying the same curated collection.</desc>
  <defs>
    <linearGradient id="stage" x2="1" y2=".35"><stop stop-color="#f5f7ff" stop-opacity=".97"/><stop offset=".36" stop-color="#e2e8f8" stop-opacity=".96"/><stop offset=".72" stop-color="#8192b7" stop-opacity=".98"/><stop offset="1" stop-color="#293b59" stop-opacity=".98"/></linearGradient>
    <radialGradient id="dusk"><stop stop-color="#263650" stop-opacity=".86"/><stop offset=".6" stop-color="#435b80" stop-opacity=".55"/><stop offset="1" stop-color="#435b80" stop-opacity="0"/></radialGradient>
    <radialGradient id="violet"><stop stop-color="#a8b4ff" stop-opacity=".36"/><stop offset="1" stop-color="#a8b4ff" stop-opacity="0"/></radialGradient>
    <radialGradient id="aqua"><stop stop-color="#72d5e3" stop-opacity=".3"/><stop offset="1" stop-color="#72d5e3" stop-opacity="0"/></radialGradient>
    <filter id="shadow" x="-8%" y="-12%" width="116%" height="132%" color-interpolation-filters="sRGB"><feDropShadow dx="0" dy="15" stdDeviation="19" flood-color="#242a48" flood-opacity=".18"/></filter>
    <clipPath id="round"><rect width="1920" height="1200" rx="32"/></clipPath>
  </defs>
  <g clip-path="url(#round)">
    <rect width="1920" height="1200" fill="url(#stage)"/>
    <ellipse cx="220" cy="180" rx="680" ry="500" fill="url(#violet)"/>
    <ellipse cx="1410" cy="360" rx="1000" ry="640" fill="url(#dusk)"/>
    <ellipse cx="1830" cy="1020" rx="700" ry="600" fill="url(#aqua)"/>
    <g fill="none" stroke="#fff" stroke-opacity=".2" stroke-width="2">
      <path d="M-80 310C450-110 1090 80 1540-130"/>
      <path d="M1300 1260C1390 980 1710 1030 2030 700"/>
      <path d="M1230 1280C1350 940 1730 1000 2060 640"/>
    </g>
    <rect x="1" y="1" width="1918" height="1198" rx="31" fill="none" stroke="#fff" stroke-opacity=".55" stroke-width="2"/>
    <!-- Brand artwork and screenshots share one continuous background. -->
    <image x="83" y="65" width="112" height="112" href="data:image/png;base64,{icon}"/>
    <text x="215" y="148" fill="#252846" font-family="Segoe UI,Arial,sans-serif" font-size="78" font-weight="650" letter-spacing="-2.4">VirtualPaper</text>
    <text x="97" y="226" fill="#68758e" font-family="Segoe UI,Arial,sans-serif" font-size="16" letter-spacing="2.2">WINDOWS · FREE · OPEN SOURCE</text>
    <image x="785" y="125" width="1075" height="663.8831" filter="url(#shadow)" href="data:image/jpeg;base64,{capture('dark')}"/>
    <image x="55" y="350" width="1240" height="765.8091" filter="url(#shadow)" href="data:image/jpeg;base64,{capture('light')}"/>
  </g>
</svg>
'''
(HERE / 'hero.svg').write_text(svg, encoding='utf-8')
print('Composed hero.svg from the official icon and unmodified real captures.')
