using System;
using System.Collections.Generic;
using System.Numerics;
using System.Drawing;

// Заглушки для несуществующих типов
namespace GameOffsets2.Native
{
    public struct Vector2i
    {
        public int X;
        public int Y;

        public Vector2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"{X},{Y}";
        }
    }
}

namespace ExileCore2
{
    // Базовые типы
    public interface IPlugin
    {
        string Name { get; }
        bool Initialise();
        void AreaChange(object area);
        void Render();
        void Tick();
    }

    public abstract class BaseSettingsPlugin<TSettings> : IPlugin where TSettings : class
    {
        public string Name { get; set; }
        public TSettings Settings { get; set; }
        public IGraphics Graphics { get; set; }
        public IGameController GameController { get; set; }
        public string DirectoryFullName { get; set; }
        public bool CanUseMultiThreading { get; set; }

        public abstract bool Initialise();
        public abstract void AreaChange(object area);
        public abstract void Render();
        public abstract void Tick();
    }

    public interface IGraphics
    {
        int Width { get; }
        int Height { get; }
        void InitImage(string textureId, string texturePath = null);
        IntPtr GetTextureId(string textureId);
    }

    public interface IGameController
    {
        ILogSystem DefaultLog { get; }
        GameState Game { get; }
    }

    public interface ILogSystem
    {
        void Info(string message);
        void Error(string message);
    }

    public class GameState
    {
        public IngameState IngameState { get; set; }
    }

    public class IngameState
    {
        public ExileCore2.PoEMemory.Elements.IngameUI IngameUi { get; set; }
    }

    namespace Shared
    {
        public class GameArea 
        {
            public string Name { get; set; }
        }

        public class RectangleF
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Width { get; set; }
            public float Height { get; set; }

            public Vector2 Center => new Vector2(X + Width / 2, Y + Height / 2);
        }

        namespace Enums
        {
            public enum MapIcon
            {
                None = 0,
                Normal,
                Magic,
                Rare,
                Unique,
                Quest
            }
        }

        namespace Nodes
        {
            public class ToggleNode
            {
                public bool Value { get; set; }
                public event Action<object, bool> OnValueChanged;

                public ToggleNode(bool value = false)
                {
                    Value = value;
                }

                public static implicit operator bool(ToggleNode node) => node.Value;
            }

            public class HotkeyNode
            {
                public System.Windows.Forms.Keys Key { get; set; }
                public Action PressedFunc { get; set; }

                public HotkeyNode(System.Windows.Forms.Keys key)
                {
                    Key = key;
                }

                public void Register() { }
                public bool PressedOnce() => false;
            }

            public class RangeNode<T> where T : struct
            {
                public T Value { get; set; }
                
                public RangeNode(T val, T min, T max)
                {
                    Value = val;
                }

                public static implicit operator T(RangeNode<T> node) => node.Value;
            }

            public class ColorNode
            {
                public Color Value { get; set; }

                public ColorNode(Color color)
                {
                    Value = color;
                }

                public static implicit operator Color(ColorNode node) => node.Value;
            }

            public class CustomNode
            {
                public Action DrawDelegate { get; set; }
            }

            public class EmptyNode { }
        }

        namespace Attributes
        {
            [AttributeUsage(AttributeTargets.Property)]
            public class MenuAttribute : Attribute
            {
                public MenuAttribute(string text = null, string tooltip = null, int parentIndex = 0) { }
            }

            [AttributeUsage(AttributeTargets.Class)]
            public class SubmenuAttribute : Attribute
            {
                public bool CollapsedByDefault { get; set; }
            }

            [AttributeUsage(AttributeTargets.Property)]
            public class ConditionalDisplayAttribute : Attribute
            {
                public ConditionalDisplayAttribute(string propertyName, object value) { }
            }
        }

        namespace Interfaces
        {
            public interface ISettings
            {
                Nodes.ToggleNode Enable { get; set; }
            }
        }
    }

    namespace PoEMemory.Elements
    {
        public class IngameUI
        {
            public WorldMapElement WorldMap { get; set; }
        }

        public class WorldMapElement
        {
            public Atlas.AtlasPanelElement AtlasPanel { get; set; }
        }

        namespace Atlas
        {
            public class AtlasPanelElement
            {
                public bool IsVisible { get; set; }
                public List<AtlasNode> Descriptions { get; set; } = new List<AtlasNode>();
            }

            public class AtlasNode
            {
                public long Address { get; set; }
                public GameOffsets2.Native.Vector2i Coordinate { get; set; }
                public AtlasNodeElement Element { get; set; }

                public override string ToString()
                {
                    return Element?.Area?.Name ?? "Unknown";
                }
            }

            public class AtlasNodeElement
            {
                public long Address { get; set; }
                public AreaData Area { get; set; }
                public bool IsVisible { get; set; }
                public bool IsVisited { get; set; }
                public bool IsActive { get; set; }
                public bool IsCompleted { get; set; }
                public bool IsUnlocked { get; set; }
                public List<ContentItem> Content { get; set; } = new List<ContentItem>();

                public RectangleF GetClientRect() => new RectangleF { X = 0, Y = 0, Width = 100, Height = 100 };
                public ElementBase GetChildAtIndex(int index) => new ElementBase();
            }

            public class ElementBase
            {
                public List<ElementBase> Children { get; set; } = new List<ElementBase>();
                public string TextureName { get; set; } = string.Empty;
            }

            public class ContentItem
            {
                public string Name { get; set; } = string.Empty;
            }

            public class AreaData
            {
                public string Name { get; set; } = string.Empty;
                public string Id { get; set; } = string.Empty;
            }

            public class RectangleF : ExileCore2.Shared.RectangleF { }
        }
    }

    // Пространство имен для Hotkey
    public class Hotkey
    {
        public System.Windows.Forms.Keys Key { get; set; }
        public Action PressedFunc { get; set; }

        public bool PressedOnce() => false;
        public void Register() { }
    }
} 