namespace NServiceBus.Azure.Transports.WindowsAzureStorageQueues
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;

    [DebuggerNonUserCode]
    static class SafeRoleEnvironment
    {
        static SafeRoleEnvironment()
        {
            try
            {
                TryLoadRoleEnvironment();
            }
            catch
            {
                IsAvailable = false;
            }
        }

        public static bool IsAvailable { get; set; } = true;

        public static string CurrentRoleInstanceId
        {
            get
            {
                ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

                var instance = roleEnvironmentType.GetProperty("CurrentRoleInstance").GetValue(null, null);
                return (string) roleInstanceType.GetProperty("Id").GetValue(instance, null);
            }
        }

        public static string DeploymentId
        {
            get
            {
                ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

                return (string) roleEnvironmentType.GetProperty("DeploymentId").GetValue(null, null);
            }
        }

        public static string CurrentRoleName
        {
            get
            {
                ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

                var instance = roleEnvironmentType.GetProperty("CurrentRoleInstance").GetValue(null, null);
                var role = roleInstanceType.GetProperty("Role").GetValue(instance, null);
                return (string) roleType.GetProperty("Name").GetValue(role, null);
            }
        }

        private static void ThrowExceptionWhenRoleEnvironmentIsNotAvailable()
        {
            if (!IsAvailable)
            {
                throw new RoleEnvironmentUnavailableException("Role environment is not available, please check IsAvailable before calling this property!");
            }
        }

        public static string GetConfigurationSettingValue(string name)
        {
            ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

            return (string) roleEnvironmentType.GetMethod("GetConfigurationSettingValue").Invoke(null, new object[]
            {
                name
            });
        }

        public static bool TryGetConfigurationSettingValue(string name, out string setting)
        {
            ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

            setting = string.Empty;
            bool result;
            try
            {
                setting = (string) roleEnvironmentType.GetMethod("GetConfigurationSettingValue").Invoke(null, new object[]
                {
                    name
                });
                result = !string.IsNullOrEmpty(setting);
            }
            catch
            {
                result = false;
            }

            return result;
        }

        public static void RequestRecycle()
        {
            ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

            roleEnvironmentType.GetMethod("RequestRecycle").Invoke(null, null);
        }

        public static string GetRootPath(string name)
        {
            ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

            var o = roleEnvironmentType.GetMethod("GetLocalResource").Invoke(null, new object[]
            {
                name
            });
            return (string) localResourceType.GetProperty("RootPath").GetValue(o, null);
        }

        public static bool TryGetRootPath(string name, out string path)
        {
            ThrowExceptionWhenRoleEnvironmentIsNotAvailable();

            bool result;
            path = string.Empty;

            try
            {
                path = GetRootPath(name);
                result = path != null;
            }
            catch
            {
                result = false;
            }

            return result;
        }

        static void TryLoadRoleEnvironment()
        {
            var serviceRuntimeAssembly = TryLoadServiceRuntimeAssembly();
            if (!IsAvailable)
            {
                return;
            }

            TryGetRoleEnvironmentTypes(serviceRuntimeAssembly);
            if (!IsAvailable)
            {
                return;
            }

            IsAvailable = IsAvailableInternal();
        }

        static void TryGetRoleEnvironmentTypes(Assembly serviceRuntimeAssembly)
        {
            try
            {
                roleEnvironmentType = serviceRuntimeAssembly.GetType("Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment");
                roleInstanceType = serviceRuntimeAssembly.GetType("Microsoft.WindowsAzure.ServiceRuntime.RoleInstance");
                roleType = serviceRuntimeAssembly.GetType("Microsoft.WindowsAzure.ServiceRuntime.Role");
                localResourceType = serviceRuntimeAssembly.GetType("Microsoft.WindowsAzure.ServiceRuntime.LocalResource");
            }
            catch (ReflectionTypeLoadException)
            {
                IsAvailable = false;
            }
        }

        static bool IsAvailableInternal()
        {
            try
            {
                return (bool) roleEnvironmentType.GetProperty("IsAvailable").GetValue(null, null);
            }
            catch
            {
                return false;
            }
        }

        static Assembly TryLoadServiceRuntimeAssembly()
        {
            try
            {
#pragma warning disable 618
                var asm = Assembly.LoadWithPartialName("Microsoft.WindowsAzure.ServiceRuntime");
#pragma warning restore 618
                IsAvailable = asm != null;
                return asm;
            }
            catch (FileNotFoundException)
            {
                IsAvailable = false;
                return null;
            }
        }

        static Type roleEnvironmentType;
        static Type roleInstanceType;
        static Type roleType;
        static Type localResourceType;
    }
}