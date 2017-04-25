﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnitTestProjectSplitter.Helpers;
using NUnitTestProjectSplitter.Splitter;

namespace NUnitTestProjectSplitter.Scanner {

	public sealed class TestAssemblyScanner {

		public IEnumerable<SplitRule> Scan( Assembly assembly, IList<SplitRule> splitRules ) {
			ISet<SplitRule> appliedRules = new HashSet<SplitRule>();

			var sw = new DebugStopwatch( "2.GetAssemblyCategories" );
			List<string> assemblyCategories = assembly
				.GetCustomAttributes<CategoryAttribute>()
				.Select( attr => attr.Name )
				.ToList();
			sw.Dispose();

			sw = new DebugStopwatch( "3.LoadTestFixturs" );
			List<TestFixture> fixtures = assembly.GetTypes()
				.Select( LoadTestFixtureOrNull )
				.Where( f => f != null )
				.ToList();
			sw.Dispose();

			using( new DebugStopwatch( "4.SplitRules.Check" ) ) {
				foreach( var fixture in fixtures ) {

					foreach( var method in fixture.TestMethods ) {

						ISet<string> testCategories = method
							.GetCustomAttributes<CategoryAttribute>( true )
							.Select( attr => attr.Name )
							.ToHashSet( StringComparer.OrdinalIgnoreCase );

						testCategories.UnionWith( assemblyCategories );
						testCategories.UnionWith( fixture.TestFixtureCategories );

						foreach( var splitRule in splitRules ) {
							if( !appliedRules.Contains( splitRule )
								&& splitRule.RequaredCategories.All( c => testCategories.Contains( c ) )
								&& splitRule.ProhibitedCategories.All( c => !testCategories.Contains( c ) ) ) {

								appliedRules.Add( splitRule );
							}
						}

					}
				}
			}

			return appliedRules;
		}

		private TestFixture LoadTestFixtureOrNull( Type type ) {

			TestFixtureAttribute[] testFixtureAttrs = type
				.GetCustomAttributes<TestFixtureAttribute>( true )
				.ToArray();

			if( !testFixtureAttrs.Any() ) {
				return null;
			}

			IEnumerable<string> testFixtureCategoryNames = testFixtureAttrs
				.Where( attr => attr.Category != null )
				.SelectMany( attr => attr.Category.Split( ',' ) );

			IEnumerable<string> categoryNames = type
				.GetCustomAttributes<CategoryAttribute>( true )
				.Select( attr => attr.Name );

			IList<string> testFixtureCategories = testFixtureCategoryNames
				.Concat( categoryNames )
				.ToList();

			BindingFlags bindingFlags = (
				BindingFlags.Public
				| BindingFlags.NonPublic
				| BindingFlags.Static
				| BindingFlags.Instance
			);

			IList<MethodInfo> methods = type.GetMethods( bindingFlags ).Where( IsTestMethod ).ToList();
			TestFixture fixture = new TestFixture( type, testFixtureCategories, methods );

			return fixture;
		}

		private static bool IsTestMethod( MethodInfo method ) {

			bool isTest = method.IsDefined( typeof( TestAttribute ), true );
			if( isTest ) {
				return true;
			}

			bool isTestCase = method.IsDefined( typeof( TestCaseAttribute ), true );
			if( isTestCase ) {
				return true;
			}

			return false;
		}

	}
}
