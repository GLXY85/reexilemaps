using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace ReExileMaps.Classes
{
    /// <summary>
    /// Адаптер для работы с новой версией ExileCore2
    /// Позволяет вызывать методы через рефлексию, если API изменилось
    /// </summary>
    public static class ExileCore2Adapter
    {
        private static readonly Dictionary<string, MethodInfo> _cachedMethods = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<string, PropertyInfo> _cachedProperties = new Dictionary<string, PropertyInfo>();
        
        /// <summary>
        /// Вызывает метод из объекта ExileCore2 по имени через рефлексию
        /// </summary>
        public static object InvokeMethod(object target, string methodName, params object[] parameters)
        {
            if (target == null) return null;
            
            string key = $"{target.GetType().FullName}.{methodName}";
            
            if (!_cachedMethods.TryGetValue(key, out var methodInfo))
            {
                methodInfo = target.GetType().GetMethod(methodName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                if (methodInfo != null)
                    _cachedMethods[key] = methodInfo;
                else
                    return null; // Метод не найден
            }
            
            try
            {
                return methodInfo.Invoke(target, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error invoking method {methodName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Получает значение свойства из объекта ExileCore2 по имени через рефлексию
        /// </summary>
        public static object GetProperty(object target, string propertyName)
        {
            if (target == null) return null;
            
            string key = $"{target.GetType().FullName}.{propertyName}";
            
            if (!_cachedProperties.TryGetValue(key, out var propertyInfo))
            {
                propertyInfo = target.GetType().GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                if (propertyInfo != null)
                    _cachedProperties[key] = propertyInfo;
                else
                    return null; // Свойство не найдено
            }
            
            try
            {
                return propertyInfo.GetValue(target);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting property {propertyName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Устанавливает значение свойства для объекта ExileCore2 по имени через рефлексию
        /// </summary>
        public static bool SetProperty(object target, string propertyName, object value)
        {
            if (target == null) return false;
            
            string key = $"{target.GetType().FullName}.{propertyName}";
            
            if (!_cachedProperties.TryGetValue(key, out var propertyInfo))
            {
                propertyInfo = target.GetType().GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                if (propertyInfo != null)
                    _cachedProperties[key] = propertyInfo;
                else
                    return false; // Свойство не найдено
            }
            
            try
            {
                propertyInfo.SetValue(target, value);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting property {propertyName}: {ex.Message}");
                return false;
            }
        }
    }
} 