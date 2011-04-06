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
		private AccurateRipVerify ar;
		private AccurateRipVerify ar2;

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

		private static AccurateRipVerify VerifyNoise(string trackoffsets, int seed, int offset)
		{
			return VerifyNoise(new CDImageLayout(trackoffsets), seed, offset);
		}

		private static AccurateRipVerify VerifyNoise(CDImageLayout toc, int seed, int offset)
		{
			var src = new NoiseGenerator(AudioPCMConfig.RedBook, toc.AudioLength * 588, seed, offset);
			var buff = new AudioBuffer(src, 588 * 10);
			var ar = new AccurateRipVerify(toc, null);
			var rnd = new Random(seed);
			while (src.Remaining > 0)
			{
				src.Read(buff, rnd.Next(1, buff.Size));
				ar.Write(buff);
			}
			return ar;
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
			ar = VerifyNoise("13 68 99 136", 2314, 0);
			ar2 = VerifyNoise("0 136 886", 2314, 0);
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
			Assert.AreEqual<uint>(3791227907, ar.CRC32(0), "CRC32[0] was not set correctly.");
			Assert.AreEqual<uint>(0321342250, ar.CRC32(1), "CRC32[1] was not set correctly.");
			Assert.AreEqual<uint>(0037001035, ar.CRC32(2), "CRC32[2] was not set correctly.");
			Assert.AreEqual<uint>(0516255430, ar.CRC32(3), "CRC32[3] was not set correctly.");

			Assert.AreEqual<uint>(3791227907, ar2.CRC32(1), "CRC32[1](2) was not set correctly.");
		}

		/// <summary>
		///A test for CRC32
		///</summary>
		[TestMethod()]
		public void CRC32Test1()
		{
			Assert.AreEqual<uint>(2953798997, ar.CRC32(0, 13), "CRC32[0][13] was not set correctly.");
			Assert.AreEqual<uint>(0480843614, ar.CRC32(0, -7), "CRC32[0][-7] was not set correctly.");
			Assert.AreEqual<uint>(1228729415, ar.CRC32(1, 13), "CRC32[1][13] was not set correctly.");
			Assert.AreEqual<uint>(3364131728, ar.CRC32(1, -7), "CRC32[1][-7] was not set correctly.");
			Assert.AreEqual<uint>(1905873074, ar.CRC32(2, 15), "CRC32[2][15] was not set correctly.");
			Assert.AreEqual<uint>(0611805314, ar.CRC32(2, -1), "CRC32[2][-1] was not set correctly.");
			Assert.AreEqual<uint>(4242272536, ar.CRC32(3, 15), "CRC32[3][15] was not set correctly.");
			Assert.AreEqual<uint>(4236330757, ar.CRC32(3, -1), "CRC32[3][-1] was not set correctly.");

			Assert.AreEqual<uint>(0480843614, ar2.CRC32(1, -7), "CRC32[1,13](2) was not set correctly.");
		}

		/// <summary>
		///A test for CRC
		///</summary>
		[TestMethod()]
		public void CRCTest1()
		{
			Assert.AreEqual<uint>(0123722587, ar.CRC(0, -1), "CRC[0][-1] was not set correctly.");
			Assert.AreEqual<uint>(1975220196, ar.CRC(0, 99), "CRC[0][99] was not set correctly.");
			Assert.AreEqual<uint>(1519928474, ar.CRC(1, -3), "CRC[1][-3] was not set correctly.");
			Assert.AreEqual<uint>(2114385036, ar.CRC(1, 11), "CRC[1][11] was not set correctly.");
			Assert.AreEqual<uint>(1521167728, ar.CRC(2, -4), "CRC[2][-4] was not set correctly.");
			Assert.AreEqual<uint>(0301435197, ar.CRC(2, 55), "CRC[2][55] was not set correctly.");
		}

		/// <summary>
		///A test for CRC
		///</summary>
		[TestMethod()]
		public void CRCTest()
		{
			Assert.AreEqual<uint>(3727147246, ar.CRC(0), "CRC[0] was not set correctly.");
			Assert.AreEqual<uint>(2202235240, ar.CRC(1), "CRC[1] was not set correctly.");
			Assert.AreEqual<uint>(3752629998, ar.CRC(2), "CRC[2] was not set correctly.");
		}

		/// <summary>
		///A test for CRCV2
		///</summary>
		[TestMethod()]
		public void CRCV2Test()
		{
			Assert.AreEqual<uint>(3988391122, ar.CRCV2(0), "CRCV2[0] was not set correctly.");
			Assert.AreEqual<uint>(2284845104, ar.CRCV2(1), "CRCV2[1] was not set correctly.");
			Assert.AreEqual<uint>(3841231027, ar.CRCV2(2), "CRCV2[2] was not set correctly.");
		}

		/// <summary>
		///A test for ARCRC offset
		///</summary>
		[TestMethod()]
		public void CRCTestOffset()
		{
			var ar0 = VerifyNoise("13 68 99 136", 723722, 0);
			for (int offs = 1; offs < 588 * 5; offs += 17)
			{
				var ar1 = VerifyNoise("13 68 99 136", 723722, offs);
				for (int track = 0; track < 3; track++)
				{
					Assert.AreEqual<uint>(ar0.CRC(track, offs), ar1.CRC(track), "CRC with offset " + offs + " was not set correctly.");
					Assert.AreEqual<uint>(ar0.CRC(track), ar1.CRC(track, -offs), "CRC with offset " + (-offs) + " was not set correctly.");
					Assert.AreEqual<uint>(ar0.CRC450(track, offs), ar1.CRC450(track, 0), "CRC450 with offset " + offs + " was not set correctly.");
					Assert.AreEqual<uint>(ar0.CRC450(track, 0), ar1.CRC450(track, -offs), "CRC450 with offset " + (-offs) + " was not set correctly.");
					if (track != 2)
						Assert.AreEqual<uint>(ar0.CRC32(track + 1, offs), ar1.CRC32(track + 1), "CRC32 with offset " + (offs) + " was not set correctly.");
					if (track != 0)
						Assert.AreEqual<uint>(ar0.CRC32(track + 1), ar1.CRC32(track + 1, -offs), "CRC32 with offset " + (-offs) + " was not set correctly.");
				}
			}
		}

		/// <summary>
		///A test for CRCWONULL
		///</summary>
		[TestMethod()]
		public void CRCWONULLTest1()
		{
			Assert.AreEqual<uint>(0812984565, ar.CRCWONULL(2, 19), "CRC32WONULL[2][19] was not set correctly.");
			Assert.AreEqual<uint>(2390392664, ar.CRCWONULL(2, -2), "CRC32WONULL[2][-2] was not set correctly.");
		}

		/// <summary>
		///A test for CRCWONULL
		///</summary>
		[TestMethod()]
		public void CRCWONULLTest()
		{
			Assert.AreEqual<uint>(0404551290, ar.CRCWONULL(0), "CRC32WONULL[0] was not set correctly.");
			Assert.AreEqual<uint>(0224527589, ar.CRCWONULL(1), "CRC32WONULL[1] was not set correctly.");
			Assert.AreEqual<uint>(0557159190, ar.CRCWONULL(2), "CRC32WONULL[2] was not set correctly.");
			Assert.AreEqual<uint>(0516255430, ar.CRCWONULL(3), "CRC32WONULL[3] was not set correctly.");
			Assert.AreEqual<uint>(0404551290, ar2.CRCWONULL(1), "CRC32WONULL[1](2) was not set correctly.");
		}

		/// <summary>
		///A test for CRC450
		///</summary>
		[TestMethod()]
		public void CRC450Test()
		{
			Assert.AreEqual<uint>(2224430043, ar2.CRC450(1, 00), "CRC450[1,00] was not set correctly.");
			Assert.AreEqual<uint>(1912726337, ar2.CRC450(1, 55), "CRC450[1,55] was not set correctly.");
			Assert.AreEqual<uint>(1251460151, ar2.CRC450(1, -3), "CRC450[1,-3] was not set correctly.");
		}


		/// <summary>
		///OffsetSafeCRCRecord
		///</summary>
		[TestMethod()]
		public void OffsetSafeCRCRecordTest()
		{
			//OffsetSafeCRCRecord[] records = new OffsetSafeCRCRecord[5000];
			var record0 = VerifyNoise("13 68 99 136", 2314, 0).OffsetSafeCRC;

			Assert.AreEqual(
				"8+lTDqEZidfayuC0LoxnL9Oluf4ywo1muFBu115XBgf254fKIdfVWZOcsQraS4eI\r\n" +
				"NoLn7W3t0a16i745nEvikfw27ZsMR7gWPrXgXdsI2OdtjWTRL2Vra2dLe3WOl/Ru\r\n" +
				"wFa1jqbB3+xHiB8XNi+5VKRh3fj1o5RSXS6tOZUvBUFFqoyuZK/DkeIyZ4gkotYO\r\n" +
				"MZSsx2JBr2tdBzHZMssUmfvWUrfJZAQD8wMv1epy7q0Mk3W/QetVz6cZZ+6rRctf\r\n" +
				"PGqvWBgNfS/+e7LBo/49KYd16kEofaX8LuuNB/7YJ85a3W71soQovwWLkjm32Xqo\r\n" +
				"KpaUagu9QED1WEx7frfu95vYsQLV+vq6zULP6QOznUpU6n6LuMPQa5WNA4+chigC\r\n" +
				"71GFeKTSO3bnS3xg8FMMqRtcTJleWF/7Bs3DkUZnxbkp4g8iZYZ3eMDc7A04AiYx\r\n" +
				"3tYvDi9WiEZMRWpvuHfoBzWU7HbfOk5+32yg8TyNyVlPq1cfFn/jwQrfNyztTyav\r\n" +
				"96ZJS2aBroYAw2We5RC2oekmi+N75L6+eQB/4iZOxB9aGP1sALd/UZaJqZP8FcmW\r\n" +
				"FJOXlBi/KW68TJvujz+2w/P7EaZ0L7llQAtoHwoJniuNN5WYXBlescGc+vyYr5df\r\n" +
				"jrul+QMmQ4xMi10mglq7CMLVfZZFFgBdvGBrn1tL9bg=\r\n",
				VerifyNoise("13 68 99 136", 2314, 13).OffsetSafeCRC.Base64);

			for (int offset = 0; offset < 5000; offset += (offset < 256 ? 1 : 37))
			{
				var record = VerifyNoise("13 68 99 136", 2314, offset).OffsetSafeCRC;

				int real_off = -offset;
				int off;
				bool found = record0.FindOffset(record, out off);
				if (real_off > 4096 || real_off < -4096)
				{
					Assert.IsFalse(found, string.Format("FindOffset found offset where it shouldn't have, real offset {0}", real_off));
				}
				else
				{
					Assert.IsTrue(found, string.Format("FindOffset failed to detect offset, real offset {0}", real_off));
					Assert.AreEqual(real_off, off, string.Format("FindOffset returned {0}, should be {1}", off, real_off));
				}
				real_off = offset;
				found = record.FindOffset(record0, out off);
				if (real_off > 4096 || real_off < -4096)
				{
					Assert.IsFalse(found, string.Format("FindOffset found offset where it shouldn't have, real offset {0}", real_off));
				}
				else
				{
					Assert.IsTrue(found, string.Format("FindOffset failed to detect offset, real offset {0}", real_off));
					Assert.AreEqual(real_off, off, string.Format("FindOffset returned {0}, should be {1}", off, real_off));
				}
			}
		}
	}


}
