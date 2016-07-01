using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CustomDI
{
    class Program
    {
        static void Main(string[] args)
        {
            Cat cat = new Cat();
            cat.Register(typeof(IFoo), typeof(Foo));
            cat.Register<IBar, Bar>();
            cat.Register<IBaz, Baz>();
            cat.Register<IQux, Qux>();

            IFoo service = cat.GetService<IFoo>();
            IBar bar = ((Foo)service).Bar;
            Baz baz = ((Foo)service).Baz as Baz;

            Console.WriteLine(service);
            Console.WriteLine("构造函数注入：" + bar);
            Console.WriteLine("属性注入：" + baz);
            Console.WriteLine("方法注入：" + baz.Qux);

            Console.Read();
        }
    }

    public interface IFoo { }
    public interface IBar { }
    public interface IBaz { }
    public interface IQux { }
    
    public class Foo : IFoo
   {
       public IBar Bar { get; private set; }
    
       [Injection]
       public IBaz Baz { get; set; }
    
       public Foo() { }
    
       [Injection]
       public Foo(IBar bar)
       {
           this.Bar = bar;
       }
   }
    
   public class Bar : IBar { }
    
   public class Baz : IBaz
   {
       public IQux Qux { get; private set; }
    
       [Injection]
       public void Initialize(IQux qux)
       {
           this.Qux = qux;
       }
   }
    
   public class Qux : IQux { }

    /// <summary>
    /// 标识是否需要被注入
    /// </summary>
    [AttributeUsage(AttributeTargets.Method|AttributeTargets.Property|AttributeTargets.Constructor,AllowMultiple =false)]
    public class InjectionAttribute:Attribute
    { }

    public class Cat
    {
        private ConcurrentDictionary<Type, Type> typeMapping = new ConcurrentDictionary<Type, Type>();
        public Cat()
        {

        }

        /// <summary>
        /// 注册类型
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void Register(Type from ,Type to)
        {
            typeMapping[from] = to;
        }

        public void Register<TFrom,TTo>()
        {
            typeMapping[typeof(TFrom)] = typeof(TTo);
        }

        public T GetService<T>()
        {
            Type type = typeof(T);
            object obj = this.GetService(type);
            return (T)obj;
        }

        public object GetService(Type serviceType)
        {
            Type type;
            if(!typeMapping.TryGetValue(serviceType,out type))
            {                
                type = serviceType;
            }
            if(type.IsInterface || type.IsAbstract)
            {
                return null;
            }

            //通过反射，获取对象的构造函数、属性、方法，并对注入做处理
            ConstructorInfo constructor = this.GetConstructor(type);
            if(null==constructor)
            { return null; }

            //获取构造函数的参数，以及使用 递归得到实参来初始化
            object[] arguments = constructor.GetParameters().Select(p => this.GetService(p.ParameterType)).ToArray();
            //获得构造函数创造的对象
            object service = constructor.Invoke(arguments);

            //处理该对象中的属性注入
            this.InitInjectedProperties(service);

            //处理该对象中的方法注入
            this.InitInjectedMethod(service);      

            return service;
        }

        public virtual ConstructorInfo GetConstructor(Type type)
        {
            //找到构造函数
            ConstructorInfo[] c = type.GetConstructors();

            //返回第一个具备注入标注的构造，没有找到，则返回第一个或默认构造函数
            return c.FirstOrDefault(s => s.GetCustomAttribute(typeof(InjectionAttribute)) != null) ?? c.FirstOrDefault();
        }

        public virtual void InitInjectedProperties(object service)
        {
            //找到具备注入标注的可写属性
            PropertyInfo[] properties = service.GetType().GetProperties()
                .Where(p => p.CanWrite && (p.GetCustomAttribute(typeof(InjectionAttribute)) != null))
                .ToArray();

            //对每一个属性进行赋值
            Array.ForEach(properties, p => {
                p.SetValue(service, this.GetService(p.PropertyType));
            });
        }
        public virtual void InitInjectedMethod(object service)
        {
            MethodInfo[] methods = service.GetType().GetMethods()
                .Where(m => m.GetCustomAttribute<InjectionAttribute>() != null)
                .ToArray();

            Array.ForEach(methods, m => {
                object[] arguments = m.GetParameters().Select(par => this.GetService(par.ParameterType)).ToArray();
                m.Invoke(service, arguments);
            });
        }
    }
}
