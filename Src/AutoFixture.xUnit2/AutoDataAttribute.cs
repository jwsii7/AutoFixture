﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using AutoFixture.Kernel;
using Xunit.Sdk;

namespace AutoFixture.Xunit2
{
    /// <summary>
    /// Provides auto-generated data specimens generated by AutoFixture as an extension to
    /// xUnit.net's Theory attribute.
    /// </summary>
    [DataDiscoverer(
        typeName: "AutoFixture.Xunit2.NoPreDiscoveryDataDiscoverer",
        assemblyName: "AutoFixture.Xunit2")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [CLSCompliant(false)]
    [SuppressMessage("Microsoft.Performance", "CA1813:AvoidUnsealedAttributes",
        Justification = "This attribute is the root of a potential attribute hierarchy.")]
    public class AutoDataAttribute : DataAttribute
    {
        private readonly Lazy<IFixture> fixtureLazy;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDataAttribute"/> class.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This constructor overload initializes the <see cref="Fixture"/> to an instance of
        /// <see cref="Fixture"/>.
        /// </para>
        /// </remarks>
        public AutoDataAttribute()
            : this(() => new Fixture())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDataAttribute"/> class with an
        /// <see cref="IFixture"/> of the supplied type.
        /// </summary>
        /// <param name="fixtureType">The type of the composer.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="fixtureType"/> does not implement <see cref="IFixture"/>
        /// or does not have a default constructor.
        /// </exception>
        [Obsolete("This constructor overload is deprecated, and will be removed in a future version of AutoFixture. If you need to change the behaviour of the [AutoData] attribute, please create a derived attribute class and pass in a customized Fixture via the constructor that takes an IFixture instance.", true)]
        public AutoDataAttribute(Type fixtureType)
            : this(CreateFixture(fixtureType))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDataAttribute"/> class with the
        /// supplied <see cref="IFixture"/>.
        /// </summary>
        /// <param name="fixture">The fixture.</param>
        [Obsolete("This constructor overload is deprecated because it offers poor performance, and will be removed in a future version. " +
                  "Please use the AutoDataAttribute(Func<IFixture> fixtureFactory) overload, so fixture will be constructed only if needed.")]
        protected AutoDataAttribute(IFixture fixture)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));

            this.fixtureLazy = new Lazy<IFixture>(() => fixture, LazyThreadSafetyMode.None);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoDataAttribute"/> class
        /// with the supplied <paramref name="fixtureFactory"/>. Fixture will be created
        /// on demand using the provided factory.
        /// </summary>
        /// <param name="fixtureFactory">The fixture factory used to construct the fixture.</param>
        protected AutoDataAttribute(Func<IFixture> fixtureFactory)
        {
            if (fixtureFactory == null) throw new ArgumentNullException(nameof(fixtureFactory));

            this.fixtureLazy = new Lazy<IFixture>(fixtureFactory, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Gets the fixture used by <see cref="GetData"/> to create specimens.
        /// </summary>
        [Obsolete("Fixture is created lazily for the performance efficiency, so this property is deprecated as it activates the fixture immediately. " +
                  "If you need to customize the fixture, do that in the factory method passed to the constructor.")]
        public IFixture Fixture => this.fixtureLazy.Value;

        /// <summary>
        /// Gets the type of <see cref="Fixture"/>.
        /// </summary>
        [Obsolete("This property is deprecated and will be removed in a future version of AutoFixture. Please use Fixture.GetType() instead.")]
        public Type FixtureType
        {
#pragma warning disable 618
            get { return this.Fixture.GetType(); }
#pragma warning restore 618
        }

        /// <summary>
        /// Returns the data to be used to test the theory.
        /// </summary>
        /// <param name="testMethod">The method that is being tested</param>
        /// <returns>The theory data generated by <see cref="Fixture"/>.</returns>
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));

            var specimens = new List<object>();
            foreach (var p in testMethod.GetParameters())
            {
                this.CustomizeFixture(p);

                var specimen = this.Resolve(p);
                specimens.Add(specimen);
            }

            return new[] { specimens.ToArray() };
        }

        private void CustomizeFixture(ParameterInfo p)
        {
            var customizeAttributes = p.GetCustomAttributes()
                .OfType<IParameterCustomizationSource>()
                .OrderBy(x => x, new CustomizeAttributeComparer());

            foreach (var ca in customizeAttributes)
            {
                var c = ca.GetCustomization(p);
#pragma warning disable 618
                this.Fixture.Customize(c);
#pragma warning restore 618
            }
        }

        private object Resolve(ParameterInfo p)
        {
#pragma warning disable 618
            var context = new SpecimenContext(this.Fixture);
#pragma warning restore 618
            return context.Resolve(p);
        }

        private static IFixture CreateFixture(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (!typeof(IFixture).GetTypeInfo().IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} is not compatible with IFixture. Please supply a Type which implements IFixture.",
                        type),
                    nameof(type));
            }

            var ctor = type.GetTypeInfo().GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} has no default constructor. Please supply a a Type that implements IFixture and has a default constructor. Alternatively you can supply an IFixture instance through one of the AutoDataAttribute constructor overloads. If used as an attribute, this can be done from a derived class.",
                        type),
                    nameof(type));
            }

            return (IFixture)ctor.Invoke(null);
        }
    }
}