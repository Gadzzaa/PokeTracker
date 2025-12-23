# 🎴 Pokémon Card Collection Manager

A modern WPF desktop application for managing your Pokémon Trading Card Game collection with real-time pricing from TCG DEX Api.

[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/en-us/download)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)](https://www.microsoft.com/en-us/software-download/windows11)
[![GitHub release](https://img.shields.io/github/v/release/gadzzaa/PokeTracker)](https://github.com/Gadzzaa/PokeTracker/releases/latest)
[![GitHub issues](https://img.shields.io/github/issues/gadzzaa/PokeTracker)](https://github.com/Gadzzaa/PokeTracker/issues)

## 🎯 Who This Is For

- Pokémon TCG collectors tracking **physical cards**
- Users who want an **offline-first**, local database
- Collectors interested in **rarity and market value**, not trading

## ✨ Features

- 📊 **Track Your Collection** - Manage cards across multiple sets/editions
- 💰 **Real-Time Pricing** - Automatic price updates from TCG DEX Api
- 📸 **Card Images** - High-quality images loaded and cached automatically
- 📈 **Copy Management** - Automatically track duplicate cards and total copies
- 🔍 **Smart Search** - Find cards by edition, rarity, and price
- 🎨 **Modern UI** - Smooth animations and intuitive interface
- 🗄️ **Configurable Database** - Easy MySQL connection setup
- 🌐 **UTF-8 Support** - International character support

## 📸 Screenshots

<p align="center">
<img src="Screenshots/Screenshot 1.png" width="800" />
</p>
<p align="center">
   <img src="Screenshots/Screenshot 2.png" width="300" />
   <img src="Screenshots/Screenshot 4.png" width="300" />
</p>

## 💖 Support

If you find this project helpful and would like to support its development:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/gadzzaa)

Your support helps keep this project maintained and improved! ☕

## 🚀 Getting Started

## 📦 Installation Methods

### Option 1: Download Release (Recommended)

1. Go to [Releases](https://github.com/gadzzaa/PokeTracker/releases)
2. Download the latest `.zip` file
3. Extract to desired location
4. Run `PokemonCardCollection.exe`
5. **Install MySQL** (if not already installed)
   - Download from [MySQL Official Site](https://dev.mysql.com/downloads/mysql/)
6. (Optional) Import a default template for card_editions into the database
   ```bash
   INSERT INTO `card_editions` (`id`, `type`, `nr_pachete`, `edition_identifier`) VALUES
   (1, 'Stellar Crown', 32, 'sv07'),
   (2, 'Phantasmal Flames', 20, 'me02'),
   (3, 'Surging Sparks', 11, 'sv08'),
   (4, 'Mega Evolutions', 30, 'me01'),
   (5, 'Obsidian Flames', 2, 'sv03'),
   (6, 'Journey Together', 7, 'sv09'),
   (7, 'White Flare', 8, 'sv10.5w'),
   (8, 'Black Bolt', 18, 'sv10.5b'),
   (9, 'Destined Rivals', 6, 'sv10');
   ```

### Option 2: Build from Source

See [Building from Source](#building-from-source) section below.

## 🔨 Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [MySQL Server 8.0+](https://dev.mysql.com/downloads/mysql/)
- Visual Studio 2022 or VS Code

### Steps

1. **Clone the repository**
   git clone <https://github.com/gadzzaa/PokeTracker.git> cd PokeTracker

2. **Install MySQL** (if not already installed)
   - Download from [MySQL Official Site](https://dev.mysql.com/downloads/mysql/)
   - Or use Docker:

     ```bash
     docker run --name mysql-pokemon -e MYSQL_ROOT_PASSWORD=your_password -p 3306:3306 -d mysql:8.0
     ```
     
3. (Optional) Import a default template for card_editions into the database
   ```bash
   INSERT INTO `card_editions` (`id`, `type`, `nr_pachete`, `edition_identifier`) VALUES
   (1, 'Stellar Crown', 32, 'sv07'),
   (2, 'Phantasmal Flames', 20, 'me02'),
   (3, 'Surging Sparks', 11, 'sv08'),
   (4, 'Mega Evolutions', 30, 'me01'),
   (5, 'Obsidian Flames', 2, 'sv03'),
   (6, 'Journey Together', 7, 'sv09'),
   (7, 'White Flare', 8, 'sv10.5w'),
   (8, 'Black Bolt', 18, 'sv10.5b'),
   (9, 'Destined Rivals', 6, 'sv10');
   ```

4. **Build the application**

   ```bash
   dotnet build
   ```

5. **Run the application**

   ```bash
   dotnet run
   ```

## ⚙️ Configuration

### First Launch

On first launch, if the database connection fails, you'll be prompted to configure:

- **Server**: Your MySQL server address (default: `localhost`)
- **Port**: MySQL port (default: `3306`)
- **Username**: MySQL username (default: `root`)
- **Password**: Your MySQL password
- **Database**: Database name (default: `pokemon_2025`)

Configuration is saved to: `%AppData%\PokemonCardCollection\dbconfig.json`

### Manual Configuration

You can access database settings anytime via the **⚙️ Database Settings** button in the application.

### Why MySQL?

MySQL was chosen for reliability, performance, and easy future expansion
(statistics, cloud sync, or multi-user support).

## 🎮 Usage

### Adding Editions

1. Click **➕ Add Edition**
2. Enter edition name (e.g., "Stellar Crown")
3. Enter edition identifier for API lookups (e.g., "sv07")
4. Click **Add Edition**

### Adding Cards

1. Select an edition from the left panel
2. Click **➕ Add New Card**
3. Enter the card number (e.g., "123")
4. The app will fetch card data from the TCG DEX Api
5. Card is automatically added with current price

### Managing Copies

- **Add Copy**: Simply add the same card again - the copy count increments
- **Remove Copy**: Use **➖ Remove Copy** button (requires 2+ copies)
- **Delete Card**: Click the **✕** button next to the card name

### Keyboard Navigation

- **Arrow Right (→)**: Move to next card
- **Arrow Left (←)**: Move to previous card
- Navigation wraps around (last card → first card and vice versa)
  
## 🔐 Privacy & Data

- No accounts required
- No cloud sync
- No analytics or tracking
- All data is stored locally in your MySQL database

## 🏗️ Built With

- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Windows Presentation Foundation
- [.NET 10](https://dotnet.microsoft.com/) - Application framework
- [MySQL](https://www.mysql.com/) - Database
- [Dapper](https://github.com/DapperLib/Dapper) - Micro ORM
- [TCG DEX Api](https://tcgdex.dev/rest) - Card data and images

## 🗄️ Database Schema

### `card_editions` Table

| Column             | Type    | Description     |
| ------------------ | ------- | --------------- |
| id                 | INT(11) | Primary key     |
| type               | TEXT    | Edition name    |
| nr_pachete         | INT(11) | Number of packs |
| edition_identifier | TEXT    | API identifier  |

### `cards` Table

| Column     | Type          | Description                  |
| ---------- | ------------- | ---------------------------- |
| id         | INT(11)       | Primary key                  |
| number     | INT(11)       | Card number                  |
| name       | TEXT          | Card name                    |
| edition_id | INT(11)       | Foreign key to card_editions |
| rarity     | TEXT          | Card rarity                  |
| price      | DECIMAL(10,2) | Current price                |
| copies     | INT(11)       | Number of copies owned       |
| image      | TEXT          | Image URL                    |
| pull_date  | DATETIME      | Date added                   |

## ⚠️ Current Limitations

- Windows-only (WPF)
- Requires a local MySQL database
- Not a marketplace or selling platform
- No mobile or cloud sync (yet)

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [TCG Dex API](https://tcgdex.dev/) for providing card data
- All contributors who help improve this project
- The Pokémon Company for creating an amazing card game

## 📞 Help

- **Issues**: [GitHub Issues](https://github.com/gadzzaa/PokeTracker/issues)
- **Discussions**: [GitHub Discussions](https://github.com/gadzzaa/PokeTracker/discussions)

## 🔮 Future Features

- [ ] Export collection to CSV/Excel
- [ ] Collection statistics and charts
- [ ] Dark/Light theme toggle
- [ ] Deck builder functionality
- [ ] Import from CSV
- [ ] Multi-user support
- [ ] Cloud backup/sync

---

Made with ❤️ for Pokémon TCG collectors

**Note**: This application is not affiliated with or endorsed by The Pokémon Company, Nintendo, or Game Freak. Pokémon is a registered trademark of The Pokémon Company.
