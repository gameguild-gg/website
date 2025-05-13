const fs = require('fs').promises;
const path = require('path');
const sharp = require('sharp');

const SOURCE_LOGO = path.join(__dirname, 'public', 'assets', 'images', 'logo-icon.png');
const ICONS_OUTPUT_DIR = path.join(__dirname, 'public', 'assets', 'images', 'icons');
const SCREENSHOTS_DIR = path.join(__dirname, 'public', 'assets', 'images', 'screenshots');

// Define the icon sizes we need for the manifest
const ICON_SIZES = [72, 96, 128, 144, 152, 192, 384, 512];

// Create placeholder screenshot files with game guild branding
const SCREENSHOTS = [
  { filename: 'screenshot1.png', width: 1280, height: 720, label: 'Dashboard' },
  { filename: 'screenshot2.png', width: 1280, height: 720, label: 'Learning Platform' }
];

// Define shortcut icon files
const SHORTCUT_ICONS = [
  { filename: 'dashboard-icon.png', size: 96 },
  { filename: 'learn-icon.png', size: 96 }
];

async function generateIcons() {
  try {
    // Ensure the output directories exist
    await fs.mkdir(ICONS_OUTPUT_DIR, { recursive: true });
    await fs.mkdir(SCREENSHOTS_DIR, { recursive: true });
    
    console.log(`Generating icons from ${SOURCE_LOGO}...`);
    
    // Generate all the required icon sizes
    for (const size of ICON_SIZES) {
      const outputFile = path.join(ICONS_OUTPUT_DIR, `icon-${size}x${size}.png`);
      await sharp(SOURCE_LOGO)
        .resize(size, size)
        .toFile(outputFile);
      console.log(`Generated ${outputFile}`);
    }
    
    // Generate shortcut icons
    for (const icon of SHORTCUT_ICONS) {
      const outputFile = path.join(ICONS_OUTPUT_DIR, icon.filename);
      await sharp(SOURCE_LOGO)
        .resize(icon.size, icon.size)
        .toFile(outputFile);
      console.log(`Generated shortcut icon: ${outputFile}`);
    }
    
    // Generate placeholder screenshots with branding
    for (const screenshot of SCREENSHOTS) {
      const outputFile = path.join(SCREENSHOTS_DIR, screenshot.filename);
      
      // Create a simple branded screenshot (dark background with text)
      await sharp({
        create: {
          width: screenshot.width,
          height: screenshot.height,
          channels: 4,
          background: { r: 24, g: 24, b: 28, alpha: 1 } // #18181c
        }
      })
      .composite([
        {
          input: {
            text: {
              text: `Game Guild - ${screenshot.label}`,
              font: 'sans-serif',
              fontSize: 48,
              rgba: true
            }
          },
          gravity: 'center'
        },
        {
          input: SOURCE_LOGO,
          gravity: 'northwest',
          top: 20,
          left: 20
        }
      ])
      .png()
      .toFile(outputFile);
      
      console.log(`Generated placeholder screenshot: ${outputFile}`);
    }
    
    console.log('All icons and screenshots generated successfully!');
    
  } catch (error) {
    console.error('Error generating icons:', error);
  }
}

generateIcons();