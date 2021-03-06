using System;
using System.Collections.Generic;
using System.Text;

namespace CUETools.Ripper.SCSI
{
	/**
	 * タイトル: RSコード・エンコーダ
	 *
	 * @author Masayuki Miyazaki
	 * http://sourceforge.jp/projects/reedsolomon/
	 */
	public class RsEncode
	{
		private static Galois galois = Galois.instance;
		private int npar;
		private int[] encodeGx;

		/**
		 * 生成多項式配列の作成
		 *		G(x)=Π[k=0,n-1](x + α^k)
		 *		encodeGxの添え字と次数の並びが逆なのに注意
		 *		encodeGx[0]        = x^(npar - 1)の項
		 *		encodeGx[1]        = x^(npar - 2)の項
		 *		...
		 *		encodeGx[npar - 1] = x^0の項
		 */
		public RsEncode(int npar)
		{
			this.npar = npar;
			encodeGx = new int[npar];
			encodeGx[npar - 1] = 1;
			for (int kou = 0; kou < npar; kou++)
			{
				int ex = galois.toExp(kou);			// ex = α^kou
				// (x + α^kou)を掛る
				for (int i = 0; i < npar - 1; i++)
				{
					// 現在の項 * α^kou + 一つ下の次数の項
					encodeGx[i] = galois.mul(encodeGx[i], ex) ^ encodeGx[i + 1];
				}
				encodeGx[npar - 1] = galois.mul(encodeGx[npar - 1], ex);		// 最下位項の計算
			}
		}

		/**
		 * RSコードのエンコード
		 *
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 *		入力データ長
		 * @param parity int[]
		 *		パリティ格納用配列
		 * @param parityStartPos int
		 *		パリティ格納用Index
		 * @return bool
		 */
		public void encode(byte[] data, int datapos, int length, byte[] parity, int parityStartPos)
		{
			if (length < 0 || length + npar > 255)
				throw new Exception("RsEncode: wrong length");

			/*
			 * パリティ格納用配列
			 * wr[0]        最上位
			 * wr[npar - 1] 最下位		なのに注意
			 * これでパリティを逆順に並べかえなくてよいので、arraycopyが使える
			 */
			byte[] wr = new byte[npar];
			for (int idx = datapos; idx < datapos + length; idx++)
			{
				int ib = wr[0] ^ data[idx];
				for (int i = 0; i < npar - 1; i++)
					wr[i] = (byte)(wr[i + 1] ^ galois.mul(ib, encodeGx[i]));
				wr[npar - 1] = (byte)galois.mul(ib, encodeGx[npar - 1]);
			}
			if (parity != null)
				Array.Copy(wr, 0, parity, parityStartPos, npar);
		}
	}
}
