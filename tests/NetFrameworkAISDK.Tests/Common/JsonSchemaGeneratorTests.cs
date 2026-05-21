using NetFrameworkAISDK.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NetFrameworkAISDK.Tests.Common
{
    [TestFixture]
    public class JsonSchemaGeneratorTests
    {
        public class SimpleType
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        public class WeatherInfo
        {
            public string City { get; set; }
            public double Temperature { get; set; }
            public string Condition { get; set; }
        }

        public enum Color { Red, Green, Blue }

        public class EnumType
        {
            public string Name { get; set; }
            public Color FavoriteColor { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
        }

        public class NestedType
        {
            public string Name { get; set; }
            public Address HomeAddress { get; set; }
        }

        public class ListType
        {
            public string Name { get; set; }
            public List<string> Tags { get; set; }
        }

        public class ArrayType
        {
            public string Name { get; set; }
            public int[] Scores { get; set; }
        }

        [Test]
        public void GenerateFromType_SimpleType_ReturnsValidSchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(SimpleType), "simple_type");

            Assert.IsTrue(schema.Contains("\"type\":\"object\""));
            Assert.IsTrue(schema.Contains("\"Name\""));
            Assert.IsTrue(schema.Contains("\"Age\""));
            Assert.IsTrue(schema.Contains("\"required\":[\"Name\",\"Age\"]"));
            Assert.IsTrue(schema.Contains("\"additionalProperties\":false"));
        }

        [Test]
        public void GenerateFromType_WeatherInfo_ReturnsCorrectPropertyTypes()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(WeatherInfo), "weather_info");

            Assert.IsTrue(schema.Contains("\"City\""));
            Assert.IsTrue(schema.Contains("\"type\":\"string\""));
            Assert.IsTrue(schema.Contains("\"Temperature\""));
            Assert.IsTrue(schema.Contains("\"type\":\"number\""));
            Assert.IsTrue(schema.Contains("\"Condition\""));
        }

        [Test]
        public void GenerateFromType_WeatherInfo_IncludesAllRequired()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(WeatherInfo), "weather_info");

            Assert.IsTrue(schema.Contains("\"required\":[\"City\",\"Temperature\",\"Condition\"]"));
        }

        [Test]
        public void GenerateFromType_EnumType_ReturnsEnumSchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(EnumType), "enum_type");

            Assert.IsTrue(schema.Contains("\"FavoriteColor\""));
            Assert.IsTrue(schema.Contains("\"enum\":[\"Red\",\"Green\",\"Blue\"]"));
        }

        [Test]
        public void GenerateFromType_NestedType_ReturnsNestedSchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(NestedType), "nested_type");

            Assert.IsTrue(schema.Contains("\"HomeAddress\""));
            Assert.IsTrue(schema.Contains("\"Street\""));
        }

        [Test]
        public void GenerateFromType_ListType_ReturnsArraySchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(ListType), "list_type");

            Assert.IsTrue(schema.Contains("\"Tags\""));
            Assert.IsTrue(schema.Contains("\"type\":\"array\""));
            Assert.IsTrue(schema.Contains("\"items\""));
        }

        [Test]
        public void GenerateFromType_ArrayType_ReturnsArraySchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(typeof(ArrayType), "array_type");

            Assert.IsTrue(schema.Contains("\"Scores\""));
            Assert.IsTrue(schema.Contains("\"type\":\"integer\""));
            Assert.IsTrue(schema.Contains("\"items\""));
        }

        [Test]
        public void GenerateFromType_NullType_ReturnsEmptySchema()
        {
            var schema = JsonSchemaGenerator.GenerateFromType(null, "test");

            Assert.AreEqual("{}", schema);
        }
    }
}