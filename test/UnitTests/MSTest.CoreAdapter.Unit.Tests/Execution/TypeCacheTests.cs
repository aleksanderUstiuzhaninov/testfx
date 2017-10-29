// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using global::MSTestAdapter.TestUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Moq;
    using static TestMethodInfoTests;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
    using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeCacheTests
    {
        private TypeCache typeCache;

        private Mock<ReflectHelper> mockReflectHelper;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.typeCache = new TypeCache(this.mockReflectHelper.Object);

            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;

            this.SetupMocks();
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
        }

        #region GetTestMethodInfo tests

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfTestMethodIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            Action a = () => this.typeCache.GetTestMethodInfo(
                null,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                false);

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfTestContextIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            Action a = () => this.typeCache.GetTestMethodInfo(testMethod, null, false);

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnNullIfClassInfoForTheMethodIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);

            Assert.IsNull(
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false));
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnNullIfLoadingTypeThrowsTypeLoadException()
        {
            var testMethod = new TestMethod("M", "System.TypedReference[]", "A", isAsync: false);

            Assert.IsNull(
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false));
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfLoadingTypeThrowsException()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
                .Throws(new Exception("Load failure"));

            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, "Unable to get type C. Error: System.Exception: Load failure");
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfTypeDoesNotHaveADefaultConstructor()
        {
            string className = typeof(DummyTestClassWithNoDefaultConstructor).FullName;
            var testMethod = new TestMethod("M", className, "A", isAsync: false);

            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, "Unable to get default constructor for class " + className);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextIfPresent()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                        testMethod,
                                        new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                        false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNotNull(testMethodInfo.Parent.TestContextProperty);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextWithAnyNameIfPresent()
        {
            var type = typeof(DummyTestClassWithTestContext2);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                        testMethod,
                                        new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                        false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNotNull(testMethodInfo.Parent.TestContextProperty);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextFromBaseClassIfPresent()
        {
            var type = typeof(DummyTestClassWithTestContextInBase);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                        testMethod,
                                        new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                        false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNotNull(testMethodInfo.Parent.TestContextProperty);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldPickTheFirstTestContextPropertyWhenMultiplePresent()
        {
            var type = typeof(DummyTestClassWithMultipleTestContext);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                        testMethod,
                                        new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                        false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNotNull(testMethodInfo.Parent.TestContextProperty);
            Assert.AreEqual("TC2", testMethodInfo.Parent.TestContextProperty.Name);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextToNullIfIncorrctTypeIsDefined()
        {
            var type = typeof(DummyTestClassWithIncorrectTestContextType);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                    testMethod,
                                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                    false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNull(testMethodInfo.Parent.TestContextProperty);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextToNullIfNotPresent()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var methodInfo = type.GetMethod("TestInit");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                                    testMethod,
                                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                                    false);

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNull(testMethodInfo.Parent.TestContextProperty);
        }

        #region Assembly Info Creation tests.

        [TestMethodV1]
        public void GetTestMethodInfoShouldAddAssemblyInfoToTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldNotThrowIfWeFailToDiscoverTypeFromAnAssembly()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), true)).Throws(new Exception());

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClassWithTestMethods), typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheAssemblyInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheAssemblyCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheAssemblyInitAndCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithInitAndCleanupMethods);
            var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfAssemblyInitHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfAssemblyCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyCleanup");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheAssemblyInfoInstanceAndReuseTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            this.mockReflectHelper.Verify(rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true), Times.Once);
            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        #endregion

        #region ClassInfo Creation tests.

        [TestMethodV1]
        public void GetTestMethodInfoShouldAddClassInfoToTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.IsNull(this.typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod);
            Assert.IsNull(this.typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheClassInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheClassCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheClassInitAndCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithInitAndCleanupMethods);
            var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfClassInitHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfClassCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyCleanup");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheTestInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("TestInit"), this.typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheTestCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("TestCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfTestInitOrCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("TestInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheTestInitializeAttributeDefinedInBaseClass()
        {
            var type = typeof(DummyDerivedTestClassWithInitializeMethods);
            var baseType = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(baseType.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(baseType.GetMethod("TestInit"), this.typeCache.ClassInfoCache.ToArray()[0].BaseTestInitializeMethodsQueue.Peek());
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheTestCleanupAttributeDefinedInBaseClass()
        {
            var type = typeof(DummyDerivedTestClassWithCleanupMethods);
            var baseType = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(baseType.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(baseType.GetMethod("TestCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].BaseTestCleanupMethodsQueue.Peek());
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldCacheClassInfoInstanceAndReuseFromCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            this.testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
        }

        #endregion

        #region Method resolution tests

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowIfTestMethodHasIncorrectSignatureOrCannotBeFound()
        {
            var type = typeof(DummyTestClassWithIncorrectTestMethodSignatures);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var expectedMessage = string.Format(
                "Method {0}.{1} does not exist.",
                testMethod.FullClassName,
                testMethod.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnTestMethodInfo()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.TestMethodOptions.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.TestMethodOptions.Executor);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnTestMethodInfoWithTimeout()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithTimeout");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
                .Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(10, testMethodInfo.TestMethodOptions.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.TestMethodOptions.Executor);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldThrowWhenTimeoutIsIncorrect()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithIncorrectTimeout");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
                .Returns(true);

            Action a = () => this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var expectedMessage =
                string.Format(
                    "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be a valid integer value and cannot be less than 0.",
                    testMethod.FullClassName,
                    testMethod.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnTestMethodInfoForMethodsAdornedWithADerivedTestMethodAttribute()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithDerivedTestMethodAttribute");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.TestMethodOptions.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.TestMethodOptions.Executor);
            Assert.IsNotNull(testMethodInfo.TestMethodOptions.Executor is DerivedTestMethodAttribute);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldSetTestContextWithCustomProperty()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithCustomProperty");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            this.typeCache.GetTestMethodInfo(testMethod, testContext, false);
            var customProperty = testContext.Properties.FirstOrDefault(p => p.Key.Equals("WhoAmI"));

            Assert.IsNotNull(customProperty);
            Assert.AreEqual("Me", customProperty.Value);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyHasSameNameAsPredefinedProperties()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithOwnerAsCustomProperty");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                 null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, false);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name,
                "Owner");
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsEmpty()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithEmptyCustomPropertyName");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, false);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsNull()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, false);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldNotAddDuplicateTestPropertiesToTestContext()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithDuplicateCustomPropertyNames");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, false);

            Assert.IsNotNull(testMethodInfo);

            // Verify that the first value gets set.
            object value;
            Assert.IsTrue(testContext.Properties.TryGetValue("WhoAmI", out value));
            Assert.AreEqual("Me", value);
        }

        [TestMethodV1]
        public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedTestClasses()
        {
            var type = typeof(DerivedTestClass);
            var methodInfo = type.GetRuntimeMethod("DummyTestMethod", new Type[] { });
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.TestMethodOptions.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.TestMethodOptions.Executor);
        }

        #endregion

        #endregion

        #region ClassInfoListWithExecutableCleanupMethods tests

        [TestMethodV1]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheIsEmpty()
        {
            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethodV1]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheDoesNotHaveTestCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(false);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethodV1]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnClassInfosWithExecutableCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(1, cleanupMethods.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), cleanupMethods.ToArray()[0].ClassCleanupMethod);
        }

        #endregion

        #region AssemblyInfoListWithExecutableCleanupMethods tests

        [TestMethodV1]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheIsEmpty()
        {
            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethodV1]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheDoesNotHaveTestCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(false);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethodV1]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnAssemblyInfosWithExecutableCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(1, cleanupMethods.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), cleanupMethods.ToArray()[0].AssemblyCleanupMethod);
        }

        #endregion

        #region ResolveExpectedExceptionHelper tests

        [TestMethodV1]
        public void ResolveExpectedExceptionHelperShouldReturnExpectedExceptionAttributeIfPresent()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithExpectedException");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
                .Returns(true);
            this.mockReflectHelper.Setup(rh => rh.ResolveExpectedExceptionHelper(methodInfo, testMethod)).Returns(expectedException);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            Assert.AreEqual(expectedException, testMethodInfo.TestMethodOptions.ExpectedException);
        }

        [TestMethodV1]
        public void ResolveExpectedExceptionHelperShouldReturnNullIfExpectedExceptionAttributeIsNotPresent()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
                .Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);

            UTF.ExpectedExceptionAttribute expectedException = new UTF.ExpectedExceptionAttribute(typeof(DivideByZeroException));

            Assert.AreEqual(null, testMethodInfo.TestMethodOptions.ExpectedException);
        }

        [TestMethodV1]
        public void ResolveExpectedExceptionHelperShouldThrowIfMultipleExpectedExceptionAttributesArePresent()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithMultipleExpectedException");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.ExpectedExceptionAttribute), false))
                .Returns(true);

            try
            {
                var testMethodInfo = this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()),
                    false);
            }
            catch (Exception ex)
            {
                var message = "The test method Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution.TypeCacheTests+DummyTestClassWithTestMethods.TestMethodWithMultipleExpectedException "
                    + "has multiple attributes derived from ExpectedExceptionBaseAttribute defined on it. Only one such attribute is allowed.";
                Assert.AreEqual(ex.Message, message);
            }
        }

        #endregion

        private void SetupMocks()
        {
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(Assembly.GetExecutingAssembly());
        }

        #region dummy implementations

        [UTF.TestClass]
        internal class DummyTestClassWithTestMethods
        {
            public UTFExtension.TestContext TestContext { get; set; }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }

            [DerivedTestMethod]
            public void TestMethodWithDerivedTestMethodAttribute()
            {
            }

            [UTF.TestMethod]
            [UTF.Timeout(10)]
            public void TestMethodWithTimeout()
            {
            }

            [UTF.TestMethod]
            [UTF.Timeout(-10)]
            public void TestMethodWithIncorrectTimeout()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("WhoAmI", "Me")]
            public void TestMethodWithCustomProperty()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("Owner", "You")]
            public void TestMethodWithOwnerAsCustomProperty()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("", "You")]
            public void TestMethodWithEmptyCustomPropertyName()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty(null, "You")]
            public void TestMethodWithNullCustomPropertyName()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("WhoAmI", "Me")]
            [UTF.TestProperty("WhoAmI", "Me2")]
            public void TestMethodWithDuplicateCustomPropertyNames()
            {
            }

            [UTF.TestMethod]
            [UTF.ExpectedException(typeof(DivideByZeroException))]
            public void TestMethodWithExpectedException()
            {
            }

            [UTF.TestMethod]
            [UTF.ExpectedException(typeof(DivideByZeroException))]
            [CustomExpectedException(typeof(ArgumentNullException), "Custom Exception")]
            public void TestMethodWithMultipleExpectedException()
            {
            }
        }

        [UTF.TestClass]
        internal class DerivedTestClass : BaseTestClass
        {
        }

        internal class BaseTestClass
        {
            [UTF.TestMethod]
            public void DummyTestMethod()
            {
            }
        }

        private class DummyTestClassWithNoDefaultConstructor
        {
            private DummyTestClassWithNoDefaultConstructor(int a)
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectTestContextType
        {
            // This is TP.TF type.
            public virtual int TestContext { get; set; }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithTestContext2
        {
            public UTFExtension.TestContext TC { get; set; }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithTestContextInBase : DummyTestClassWithTestContext2
        {
        }

        [UTF.TestClass]
        private class DummyTestClassWithMultipleTestContext : DummyTestClassWithTestContext2
        {
            public UTFExtension.TestContext TC2 { get; set; }

            public UTFExtension.TestContext TC3 { get; set; }
        }

        [UTF.TestClass]
        private class DummyTestClassWithInitializeMethods
        {
            public static void AssemblyInit(UTFExtension.TestContext tc)
            {
            }

            public void TestInit()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithCleanupMethods
        {
            public static void AssemblyCleanup()
            {
            }

            public void TestCleanup()
            {
            }
        }

        [UTF.TestClass]
        private class DummyDerivedTestClassWithInitializeMethods : DummyTestClassWithInitializeMethods
        {
            public void TestMehtod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyDerivedTestClassWithCleanupMethods : DummyTestClassWithCleanupMethods
        {
            public void TestMehtod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithInitAndCleanupMethods
        {
            public static void AssemblyInit(UTFExtension.TestContext tc)
            {
            }

            public static void AssemblyCleanup()
            {
            }

            public void TestInitOrCleanup()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectInitializeMethods
        {
            public static void TestInit(int i)
            {
            }

            public void AssemblyInit(UTFExtension.TestContext tc)
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectCleanupMethods
        {
            public static void TestCleanup(int i)
            {
            }

            public void AssemblyCleanup()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectTestMethodSignatures
        {
            public static void TestMethod()
            {
            }
        }

        private class DerivedTestMethodAttribute : UTF.TestMethodAttribute
        {
        }

        #endregion
    }
}
