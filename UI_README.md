# Obsidian Launcher UI

The Obsidian Launcher now features a modern Avalonia UI inspired by MultiMC and Prism Launcher.

## UI Features

### Main Window Layout
```
┌─────────────────────────────────────────────────────────────────┐
│ [Launch] [Create Instance] [Delete Instance]    [Settings]     │
├─────────────────────────────────────────────────────────────────┤
│ Instances        │                                             │
│ ┌─────────────┐  │ Instance Details                            │
│ │ MC 1.20.4   │  │                                             │
│ │ Release     │  │ Minecraft 1.20.4                           │
│ └─────────────┘  │ Minecraft Release                           │
│ ┌─────────────┐  │                                             │
│ │ MC 1.19.4   │  │ Instance Information                        │
│ │ Release     │  │ ┌─────────────────────────────────────────┐ │
│ └─────────────┘  │ │ Name: Minecraft 1.20.4                 │ │
│ ┌─────────────┐  │ │ Version: 1.20.4                        │ │
│ │ MC 1.18.2   │  │ │ Type: Release                          │ │
│ │ Release     │  │ └─────────────────────────────────────────┘ │
│ └─────────────┘  │                                             │
│                  │ News and Updates                            │
│                  │ ┌─────────────────────────────────────────┐ │
│                  │ │ Welcome to Obsidian Launcher!          │ │
│                  │ │ This is where we'll show Minecraft     │ │
│                  │ │ news and launcher updates.             │ │
│                  │ └─────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│ Ready                                            [Progress Bar] │
└─────────────────────────────────────────────────────────────────┘
```

### Key Features

1. **Three-Panel Layout**: Similar to MultiMC/Prism Launcher
   - Top toolbar with action buttons
   - Left sidebar for instance list
   - Main content area for instance details and news

2. **Instance Management**
   - Visual list of Minecraft instances
   - Launch, create, and delete functionality
   - Instance details display

3. **Modern Design**
   - Fluent Design System styling
   - Responsive layout with splitters
   - Progress indicators for operations

4. **MVVM Architecture**
   - Clean separation of concerns
   - ReactiveUI for reactive programming
   - Proper data binding

### Technical Implementation

- **Framework**: Avalonia UI 11.2.0 (cross-platform)
- **Pattern**: MVVM with ReactiveUI
- **Services**: Integrated with existing launcher services
- **Styling**: Fluent theme with custom Obsidian branding

The UI is fully functional and integrates with the existing launcher backend services for a complete Minecraft launching experience.