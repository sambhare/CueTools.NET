﻿// The following code was generated by Microsoft Visual Studio 2005.
// The test owner should check each test for validity.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.Collections.Generic;
using CUETools.CDImage;
using CUETools.AccurateRip;
using CUETools.Codecs;
namespace CUETools.TestCodecs
{
	/// <summary>
	///This is a test class for CUETools.AccurateRip.AccurateRipVerify and is intended
	///to contain all CUETools.AccurateRip.AccurateRipVerify Unit Tests
	///</summary>
	[TestClass()]
	public class AccurateRipVerifyTest
	{


		private TestContext testContextInstance;
		private CDImageLayout toc;
		private AccurateRipVerify ar;

		/// <summary>
		///Gets or sets the test context which provides
		///information about and functionality for the current test run.
		///</summary>
		public TestContext TestContext
		{
			get
			{
				return testContextInstance;
			}
			set
			{
				testContextInstance = value;
			}
		}
		#region Additional test attributes
		// 
		//You can use the following additional attributes as you write your tests:
		//
		//Use ClassInitialize to run code before running the first test in the class
		//
		//[ClassInitialize()]
		//public static void MyClassInitialize(TestContext testContext)
		//{
		//}
		//
		//Use ClassCleanup to run code after all tests in a class have run
		//
		//[ClassCleanup()]
		//public static void MyClassCleanup()
		//{
		//}
		//
		//Use TestInitialize to run code before running each test
		//
		[TestInitialize()]
		public void MyTestInitialize()
		{
			toc = new CDImageLayout();
			toc.AddTrack(new CDTrack(1, 03, 20, true, false));
			toc.AddTrack(new CDTrack(2, 23, 20, true, false));
			toc.AddTrack(new CDTrack(3, 43, 20, true, false));
			ar = new AccurateRipVerify(toc);
			for (int sector = 0; sector < toc.AudioLength; sector++)
			{
				AudioBuffer buff = new AudioBuffer(AudioPCMConfig.RedBook, 588);
				buff.Length = 588;
				for (int i = 0; i < buff.Length; i++)
				{
					buff.Samples[i, 0] = sector * 588 + i;
					buff.Samples[i, 1] = sector * 588;
				}
				ar.Write(buff);
			}
		}
		
		//Use TestCleanup to run code after each test has run
		//
		//[TestCleanup()]
		//public void MyTestCleanup()
		//{
		//}
		//
		#endregion


		/// <summary>
		///A test for CRC32
		///</summary>
		[TestMethod()]
		public void CRC32Test()
		{
			Assert.AreEqual<uint>(3233779629, ar.CRC32(0), "CRC32[0] was not set correctly.");
			Assert.AreEqual<uint>(0408974480, ar.CRC32(1), "CRC32[1] was not set correctly.");
			Assert.AreEqual<uint>(4123211700, ar.CRC32(2), "CRC32[2] was not set correctly.");
			Assert.AreEqual<uint>(0564210909, ar.CRC32(3), "CRC32[3] was not set correctly.");
		}

		/// <summary>
		///A test for CRC32
		///</summary>
		[TestMethod()]
		public void CRC32Test1()
		{
			Assert.AreEqual<uint>(3753760724, ar.CRC32(0, 13), "CRC32[0][13] was not set correctly.");
			Assert.AreEqual<uint>(3153592639, ar.CRC32(0, -7), "CRC32[0][-7] was not set correctly.");
			Assert.AreEqual<uint>(0297562037, ar.CRC32(2, 15), "CRC32[2][15] was not set correctly.");
			Assert.AreEqual<uint>(0398293317, ar.CRC32(2, -1), "CRC32[2][-1] was not set correctly.");
		}

		/// <summary>
		///A test for CRC
		///</summary>
		[TestMethod()]
		public void CRCTest1()
		{
			Assert.AreEqual<uint>(3206462296, ar.CRC(1, 11), "CRC[1][11] was not set correctly.");
		}

		/// <summary>
		///A test for CRC
		///</summary>
		[TestMethod()]
		public void CRCTest()
		{
			Assert.AreEqual<uint>(3762425816, ar.CRC(0), "CRC[0] was not set correctly.");
			Assert.AreEqual<uint>(3103217968, ar.CRC(1), "CRC[1] was not set correctly.");
			Assert.AreEqual<uint>(3068174852, ar.CRC(2), "CRC[2] was not set correctly.");
		}

		/// <summary>
		///A test for CRCWONULL
		///</summary>
		[TestMethod()]
		public void CRCWONULLTest1()
		{
			Assert.AreEqual<uint>(0062860870, ar.CRCWONULL(2, 19), "CRC32WONULL[2][19] was not set correctly.");
			Assert.AreEqual<uint>(0950746738, ar.CRCWONULL(2, -2), "CRC32WONULL[2][-2] was not set correctly.");
		}

		/// <summary>
		///A test for CRCWONULL
		///</summary>
		[TestMethod()]
		public void CRCWONULLTest()
		{
			Assert.AreEqual<uint>(2395016718, ar.CRCWONULL(0), "CRC32WONULL[0] was not set correctly.");
			Assert.AreEqual<uint>(0834934371, ar.CRCWONULL(1), "CRC32WONULL[1] was not set correctly.");
			Assert.AreEqual<uint>(4123211700, ar.CRCWONULL(2), "CRC32WONULL[2] was not set correctly.");
			Assert.AreEqual<uint>(0564210909, ar.CRCWONULL(3), "CRC32WONULL[3] was not set correctly.");
		}
	}


}
