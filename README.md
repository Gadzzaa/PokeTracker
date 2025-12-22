# 🎴 Pokémon Card Collection Manager

A modern WPF desktop application for managing your Pokémon Trading Card Game collection with real-time pricing from the Pokémon TCG API.

![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

- 📊 **Track Your Collection** - Manage cards across multiple sets/editions
- 💰 **Real-Time Pricing** - Automatic price updates from Pokémon TCG API
- 📸 **Card Images** - High-quality images loaded and cached automatically
- 📈 **Copy Management** - Track multiple copies of the same card
- 🔍 **Smart Search** - Find cards by edition, rarity, and price
- 🎨 **Modern UI** - Smooth animations and intuitive interface
- 🗄️ **Configurable Database** - Easy MySQL connection setup
- 🌐 **UTF-8 Support** - International character support

## 📸 Screenshots

*(Add screenshots here)*

## 🚀 Getting Started

### Prerequisites

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MySQL Server 8.0+ (local or remote)

### Installation

1. **Clone the repository**
git clone https://github.com/yourusername/pokemon-card-collection.git cd pokemon-card-collection

2. **Install MySQL** (if not already installed)
   - Download from [MySQL Official Site](https://dev.mysql.com/downloads/mysql/)
   - Or use Docker:
     ```bash
     docker run --name mysql-pokemon -e MYSQL_ROOT_PASSWORD=your_password -p 3306:3306 -d mysql:8.0
     ```

3. **Build the application**
dotnet build

4. **Run the application**
dotnet run


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
4. The app will fetch card data from the Pokémon TCG API
5. Card is automatically added with current price

### Managing Copies

- **Add Copy**: Simply add the same card again - the copy count increments
- **Remove Copy**: Use **➖ Remove Copy** button (requires 2+ copies)
- **Delete Card**: Click the **✕** button next to the card name

### Keyboard Navigation

- **Arrow Right (→)**: Move to next card
- **Arrow Left (←)**: Move to previous card
- Navigation wraps around (last card → first card and vice versa)


## 🏗️ Built With

- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Windows Presentation Foundation
- [.NET 10](https://dotnet.microsoft.com/) - Application framework
- [MySQL](https://www.mysql.com/) - Database
- [Dapper](https://github.com/DapperLib/Dapper) - Micro ORM
- [TCG DEX Api](https://tcgdex.dev/rest) - Card data and images


## 🗄️ Database Schema

### `card_editions` Table
| Column | Type | Description |
|--------|------|-------------|
| id | INT(11) | Primary key |
| type | TEXT | Edition name |
| nr_pachete | INT(11) | Number of packs |
| edition_identifier | TEXT | API identifier |

### `cards` Table
| Column | Type | Description |
|--------|------|-------------|
| id | INT(11) | Primary key |
| number | INT(11) | Card number |
| name | TEXT | Card name |
| edition_id | INT(11) | Foreign key to card_editions |
| rarity | TEXT | Card rarity |
| price | DECIMAL(10,2) | Current price |
| copies | INT(11) | Number of copies owned |
| image | TEXT | Image URL |
| pull_date | DATETIME | Date added |

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 💖 Support

If you find this project helpful and would like to support its development:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/gadzzaa)

Your support helps keep this project maintained and improved! ☕

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [TCG Dex API](https://tcgdex.dev/) for providing card data
- All contributors who help improve this project
- The Pokémon Company for creating an amazing card game

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/PokemonInventory_Tracker/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/PokemonInventory_Tracker/discussions)
- **Ko-fi**: [Support the project](https://ko-fi.com/gadzzaa)

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