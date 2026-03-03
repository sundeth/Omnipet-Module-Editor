# Omnipet Module Editor

[![Build and Release](https://github.com/sundeth/Omnipet-Module-Editor/actions/workflows/build.yml/badge.svg)](https://github.com/sundeth/Omnipet-Module-Editor/actions/workflows/build.yml)

A comprehensive module editor for the Omnipet virtual pet platform, allowing creators to design and customize pet modules with extensive configuration options.

## Features

### Module Configuration
- **General Settings**: Configure module name, version, description, author, and category
- **Ruleset Support**: Multiple ruleset options (DMC, PENC, DMX, VB)
- **Battle Systems**: Configure battle protocols, minigames, and battle mechanics
- **Adventure Mode**: Support for adventure mode with customizable styles

### Pet Management
- **Pet Editor**: Create and edit pets with detailed attributes
  - Basic stats (HP, Power, Energy, Weight, etc.)
  - Evolution settings and requirements
  - Attack animations and sleep schedules
  - Special pets with unique keys
  - Version and index-based organization
- **Pet Sorting**: Automatic sorting by version, index, stage, and name
- **Evolution Editor**: Configure evolution paths and requirements
- **Sprite Management**: Import and manage pet sprites with HD support

### Care System
- **Meat Care**: Weight gain, hunger gain, care mistakes
- **Protein System**: Strength gain, DP gain, overdose mechanics
- **Sleep Management**: Sleep schedules, back-to-sleep timers, disturbance penalties
- **Poop Management**: Configurable poop chance percentages (single, double, triple, giga)
- **Overfeed System**: Timer-based overfeed tracking
- **Condition Hearts**: Optional 4-heart fixed system

### Training & Battle
- **Training System**: Effort gain, strength multipliers, weight changes
- **Battle Configuration**: Sick chances, attribute advantages, global hit points
- **Battle Protocols**: Support for various Digimon device protocols (DM20, PEN20, DMX, PENZ, DMC, DM)
- **Sequential Rounds**: Optional sequential battle round system

### Death & Vital Systems
- **Death Conditions**: Configurable death triggers (injuries, care mistakes, timers)
- **Vital Values**: Base values and loss rates
- **Old Age System**: Configurable old age death threshold
- **Save Mechanics**: B-press and shake-based save options

### Items & Boosts
- **Item Boost Limits**: Configure max HP, Attack, and Power boosts from items
- **G-Cell System**: Optional G-Cell currency system with configurable rewards

### Unlocks & Backgrounds
- **Unlock System**: Multiple unlock types (egg, adventure, evolution, digidex, battle, group, PVP, versus)
- **Group Unlocks**: Combine multiple unlock requirements
- **Background Manager**: Add and manage backgrounds with day/night cycle support
- **Hi-Res Validation**: Automatic validation of high-resolution background assets

### UI & Display
- **Visible Stats Configuration**: Customize which stats are visible in the game UI
- **High Definition Sprites**: Support for HD sprite assets
- **Sprite Management**: Import, refresh, and validate sprite assets

## System Requirements

- **Framework**: .NET Framework 4.7.2
- **Language**: C# 7.3
- **Platform**: Windows
- **IDE**: Visual Studio 2017 or later (recommended)

## Getting Started

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/sundeth/Omnipet-Module-Editor.git
   cd Omnipet-Module-Editor
   ```

2. Open the solution:
   - Open `OmnipetModuleEditor.sln` in Visual Studio
   - Restore NuGet packages (automatic in most cases)

3. Build the project:
   - Press `F6` or select `Build > Build Solution`
   - The executable will be in `bin/Debug` or `bin/Release`

### Using Pre-built Releases

Download the latest release from the [Releases](https://github.com/sundeth/Omnipet-Module-Editor/releases) page.

**Automatic Builds**: Every push to the `main` branch triggers an automatic build via GitHub Actions. Build artifacts are available in the Actions tab for 30 days.

## OmniNet Integration

The module editor includes OmniNet integration for publishing and sharing modules online.

### Server Configuration
- **Production (Release builds)**: `https://omnipet.app.br`
- **Development (Debug builds)**: Tries `localhost:8000` first, then falls back to `https://dev.omnipet.app.br`

For more details on server configuration, see [docs/OMNINET_CONFIGURATION.md](docs/OMNINET_CONFIGURATION.md).

## Usage

1. **Open or Create Module**: Launch the application and either open an existing module or create a new one
2. **Configure Module Settings**: Use the Main tab to set up basic module information
3. **Add Pets**: Navigate to the Pets tab to create and configure pets
4. **Set Up Unlocks**: Use the Unlocks tab to define unlock conditions
5. **Add Backgrounds**: Manage background images in the Backgrounds tab
6. **Save**: Your changes are automatically saved when you click Save buttons

## Project Structure

```
OmnipetModuleEditor/
??? tabs/                    # Main UI tabs (ModuleTab, PetTab, etc.)
??? models/                  # Data models (Module, Pet, Unlock, Background)
??? utils/                   # Utility classes (PetUtils, SpriteUtils)
??? controls/                # Custom UI controls
??? docgenerators/           # Documentation generators
??? omninet/                 # Online features (API client, forms)
??? template/                # HTML templates for documentation
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Guidelines

- Follow the existing code style and conventions
- Ensure all builds pass before submitting PR
- Update documentation as needed
- Add comments for complex logic

## Versioning

This project uses semantic versioning. For the versions available, see the [tags on this repository](https://github.com/sundeth/Omnipet-Module-Editor/tags).

## Authors

- **Sundeth** - *Initial work* - [sundeth](https://github.com/sundeth)

## License

[Specify your license here - e.g., MIT, GPL, etc.]

## Acknowledgments

- Thanks to the Omnipet community for feedback and support
- Built with .NET Framework and Windows Forms

## Support

For bugs, feature requests, or questions, please [open an issue](https://github.com/sundeth/Omnipet-Module-Editor/issues) on GitHub.
