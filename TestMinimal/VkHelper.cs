using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Vulkan;

unsafe class VkApplication : IDisposable
{
    Vk api = Vk.GetApi();
    MemoryHandle? appName;
    MemoryHandle? engineName;
    PhysicalDeviceMemoryProperties memProperties;
    Device device;
    Queue graphicsQueue;
    public Device Device => device;
    public Vk Api => api;
    private static T* Null<T>() where T : struct => (T*)IntPtr.Zero;
    public VkApplication(string appname, string enginename)
    {
        Memory<byte> appName = Encoding.ASCII.GetBytes(appname);
        Memory<byte> engineName = Encoding.ASCII.GetBytes(enginename);

        this.appName = appName.Pin();
        this.engineName = appName.Pin();
        ApplicationInfo appinfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)this.appName.Value.Pointer,
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)this.engineName.Value.Pointer,
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version11
        };
        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appinfo
        };

        try
        {
            if (Api.CreateInstance(createInfo, null, out var instance) != Result.Success)
            {
                throw new Exception("failed to create instance!");
            }

            uint count = 0;
            Api.EnumeratePhysicalDevices(instance, ref count, Null<PhysicalDevice>());//(PhysicalDevice*)IntPtr.Zero.ToPointer());
            Span<PhysicalDevice> devices = stackalloc PhysicalDevice[(int)count];
            Api.EnumeratePhysicalDevices(instance, &count, devices);
            PhysicalDevice physicalDevice = default;
            for (int i = 0; i < count; ++i)
            {
                var props = Api.GetPhysicalDeviceProperties(devices[i]);
                Console.WriteLine(Marshal.PtrToStringAuto((nint)props.DeviceName));
                // Console.WriteLine();
                var feats = Api.GetPhysicalDeviceFeatures(devices[i]);
                if (props.DeviceType == PhysicalDeviceType.DiscreteGpu)
                {
                    physicalDevice = devices[i];
                    break;
                }
            }
            if (physicalDevice.Handle == 0) throw new Exception("GPU's not found");
            PhysicalDeviceMemoryProperties memProperties;
            Api.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);
            this.memProperties = memProperties;

            uint queueFamilityCount = 0;
            Api.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilityCount, Null<QueueFamilyProperties>());

            Span<QueueFamilyProperties> queueFamilyProperties = stackalloc QueueFamilyProperties[(int)queueFamilityCount];
            Console.WriteLine($"queues = {queueFamilityCount}");
            Api.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilityCount, queueFamilyProperties);
            uint qfi = 0xFFFFFFFF;
            for (int i = 0; i < queueFamilyProperties.Length; ++i)
            {
                if (queueFamilyProperties[i].QueueFlags.HasFlag(QueueFlags.ComputeBit))
                {
                    qfi = (uint)i;
                    break;
                }
            }
            if (qfi == 0xFFFFFFFF) throw new Exception("Queue's not found");
            var priority = 1.0f;
            DeviceQueueCreateInfo queueCreateInfo = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = qfi,
                QueueCount = 1,
                PQueuePriorities = &priority,
            };
            PhysicalDeviceFeatures deviceFeatures = new();

            DeviceCreateInfo deviceCreateInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = 0,
                EnabledLayerCount = 0,
            };

            if (Api.CreateDevice(physicalDevice, in deviceCreateInfo, null, out device) != Result.Success)
            {
                throw new Exception("failed to create logical device!");
            }
            Api.GetDeviceQueue(device, qfi, 0, out graphicsQueue);
        }
        catch
        {
            Dispose();
            throw;
        }
    }
    public uint MemoryType(uint memoryTypeBits, MemoryPropertyFlags properties)
    {
        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((memoryTypeBits & (1U << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }
        throw new Exception("Memory type is not found/supported by device!");
    }
    public void Dispose()
    {
        appName?.Dispose();
        appName = null;
        engineName?.Dispose();
        engineName = null;
    }
    public class DeviceBuffer : IDisposable
    {
        Silk.NET.Vulkan.Buffer? buffer;
        DeviceMemory? memory;
        VkApplication app;
        public DeviceBuffer(VkApplication application, uint size, BufferUsageFlags usage, MemoryPropertyFlags properties)
        {
            app = application;
            BufferCreateInfo bufferInfo = new BufferCreateInfo()
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
            };
            try
            {
                Silk.NET.Vulkan.Buffer buffer;
                if (app.Api.CreateBuffer(app.Device, &bufferInfo, Null<AllocationCallbacks>(), out buffer) != Result.Success)
                {
                    throw new Exception("Failed to create buffer!");
                }
                this.buffer = buffer;
                MemoryRequirements memRequirements;
                app.Api.GetBufferMemoryRequirements(app.device, buffer, &memRequirements);

                MemoryAllocateInfo allocInfo = new MemoryAllocateInfo()
                {
                    SType = StructureType.MemoryAllocateInfo,
                    AllocationSize = memRequirements.Size,
                    MemoryTypeIndex = app.MemoryType(memRequirements.MemoryTypeBits, properties),
                };

                DeviceMemory bufferMemory;
                if (app.Api.AllocateMemory(app.Device, &allocInfo, Null<AllocationCallbacks>(), &bufferMemory) != Result.Success)
                {
                    throw new Exception("Failed to allocate buffer memory!");
                }
                memory = bufferMemory;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Bind()
        {
            if (buffer == null || memory == null) return;
            app.Api.BindBufferMemory(app.Device, buffer.Value, memory.Value, 0);
        }
        public void Write(Span<byte> data)
        {
            if (buffer == null || memory == null) return;
            Bind();
            void* bdata = (void*)IntPtr.Zero;
            if (app.Api.MapMemory(app.Device, memory.Value, 0, (uint)data.Length, 0, ref bdata) != Result.Success)
            {
                throw new Exception("Failed to map memory");
            }
            try
            {
                data.CopyTo(new Span<byte>(bdata, data.Length));
            }
            catch
            {
                app.Api.UnmapMemory(app.Device, memory.Value);
                throw;
            }
        }

        public void Dispose()
        {
            if (buffer != null) { app.Api.DestroyBuffer(app.Device, buffer.Value, Null<AllocationCallbacks>()); buffer = null; }
            if (memory != null) { app.Api.FreeMemory(app.Device, memory.Value, Null<AllocationCallbacks>()); memory = null; }
        }
    }
    public class ShaderProgram : IDisposable
    {
        ShaderModule? module;
        VkApplication app;
        public ShaderProgram(VkApplication application, Span<byte> binary)
        {
            app = application;
            fixed (byte* code = binary)
            {
                ShaderModuleCreateInfo createInfo = new()
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (uint)binary.Length,
                    PCode = (uint*)code
                };
                ShaderModule shaderModule;
                if (app.Api.CreateShaderModule(app.Device, &createInfo, Null<AllocationCallbacks>(), &shaderModule) != Result.Success)
                {
                    throw new Exception("failed to create shader module!");
                }
            }
        }
        public void Dispose()
        {
            if (module != null) { app.Api.DestroyShaderModule(app.Device, module.Value, Null<AllocationCallbacks>()); module = null; }
        }
    }
    public DeviceBuffer CreateBuffer(uint size, BufferUsageFlags usage, MemoryPropertyFlags properties) => new(this, size, usage, properties);
    public ShaderProgram CreateShader(Span<byte> binary) => new(this, binary);

}