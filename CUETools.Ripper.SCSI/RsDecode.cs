using System;
using System.Collections.Generic;
using System.Text;

namespace CUETools.Ripper.SCSI
{
	/**
	 * タイトル: RSコード・デコーダ
	 *
	 * @author Masayuki Miyazaki
	 * http://sourceforge.jp/projects/reedsolomon/
	 */
	public class RsDecode
	{
		public const int RS_CORRECT_ERROR = -2;
		private static Galois galois = Galois.instance;
		private int npar;

		public RsDecode(int npar)
		{
			this.npar = npar;
		}

		/**
		 * Modified Berlekamp-Massey
		 *
		 * @param sigma int[]
		 *		σ(z)格納用配列、最大npar/2 + 2個の領域が必要
		 * 		σ0,σ1,σ2, ... σ<jisu>
		 * @param omega int[]
		 *		ω(z)格納用配列、最大npar/2 + 1個の領域が必要
		 *		ω0,ω1,ω2, ... ω<jisu-1>
		 * @param syn int[]
		 *		シンドローム配列
		 * 		s0,s1,s2, ... s<npar-1>
		 * @return int
		 * 		>= 0: σの次数
		 * 		< 0: エラー
		 */
		public int calcSigmaMBM(int[] sigma, int[] omega, int[] syn)
		{
			int[] sg0 = new int[npar];
			int[] sg1 = new int[npar];

			sg0[1] = 1;
			sg1[0] = 1;
			int jisu0 = 1;
			int jisu1 = 0;
			int m = -1;

			for (int n = 0; n < npar; n++)
			{
				// 判別式を計算
				int d = syn[n];
				for (int i = 1; i <= jisu1; i++)
				{
					d ^= galois.mul(sg1[i], syn[n - i]);
				}
				if (d != 0)
				{
					int logd = galois.toLog(d);
					int[] wk = new int[npar];
					for (int i = 0; i <= n; i++)
					{
						wk[i] = sg1[i] ^ galois.mulExp(sg0[i], logd);
					}
					int js = n - m;
					if (js > jisu1)
					{
						m = n - jisu1;
						jisu1 = js;
						if (jisu1 > npar / 2)
						{
							return -1;				// σの次数がnpar / 2を超えたらエラー
						}
						for (int i = 0; i <= jisu0; i++)
						{
							sg0[i] = galois.divExp(sg1[i], logd);
						}
						jisu0 = jisu1;
					}
					sg1 = wk;
				}
				Array.Copy(sg0, 0, sg0, 1, Math.Min(sg0.Length - 1, jisu0));
				sg0[0] = 0;
				jisu0++;
			}
			galois.mulPoly(omega, sg1, syn);
			Array.Copy(sg1, 0, sigma, 0, Math.Min(sg1.Length, sigma.Length));
			return jisu1;
		}

		/**
		 * 最終エラー位置のセット
		 * @param pos
		 * 		誤り位置格納用初列
		 * @param n
		 * 		データ長
		 * @param last
		 * 		エラー位置
		 * @return
		 * 		0: 正常終了
		 * 		< 0: エラー
		 */
		private bool setLastErrorPos(int[] pos, int n, int last)
		{
			if (galois.toLog(last) >= n)
				return false;	// 範囲外なのでエラー
			pos[0] = last;
			return true;
		}

		/**
		 * チェン探索により誤り位置を求める
		 *		σ(z) = 0の解を探索する
		 *		ただし、探索はデータ長以内の解のみで
		 *		jisu個の解が見つからなければ、エラーとする
		 * @param pos int[]
		 * 		誤り位置格納用配列、jisu個の領域が必要
		 * @param n int
		 * 		データ長
		 * @param jisu int
		 * 		σの次数
		 * @param sigma int[]
		 * 		σ0,σ1,σ2, ... σ<jisu>
		 * @return int
		 *		0: 正常終了
		 *		< 0: エラー
		 */
		private bool chienSearch(int[] pos, int n, int jisu, int[] sigma)
		{
			/*
			 * σ(z) = (1-α^i*z)(1-α^j*z)(1-α^k*z)
			 *       = 1 + σ1z + σ2z^2 +...
			 * σ1 = α^i + α^j + α^k
			 * つまりσ1は全ての解の合計となっている。上記の性質を利用して、プチ最適化
			 * last = σ1から、見つけた解を次々と引いていくことにより、最後の解はlastとなる
			 */
			int last = sigma[1];
			if (jisu == 1)
			{
				// 次数が1ならばlastがその解である
				return setLastErrorPos(pos, n, last);
			}
			int posIdx = jisu - 1;		// 誤り位置格納用インデックス
			for (int i = 0; i < n; i++)
			{
				/*
				 * σ(z)の計算
				 * w を1(0乗の項)に初期化した後、残りの項<1..jisu>を加算
				 * z = 1/α^i = α^Iとすると
				 * σ(z) = 1 + σ1α^I + σ2(α^I)^2 + σ3(α^I)^3 + ... + σ<jisu>/(α^I)^<jisu>
				 *      = 1 + σ1α^I + σ2α^(I*2) + σ3α^(I*3) + ... + σ<jisu>α^(I*<jisu>)
				 */
				int z = 255 - i;					// z = 1/α^iのスカラー
				int wk = 1;
				for (int j = 1; j <= jisu; j++)
				{
					wk ^= galois.mulExp(sigma[j], (z * j) % 255);
				}
				if (wk == 0)
				{
					int pv = galois.toExp(i);		// σ(z) = 0の解
					last ^= pv;					// lastから今見つかった解を引く
					pos[posIdx--] = pv;
					if (posIdx == 0)
					{
						// 残りが一つならば、lastがその解である
						return setLastErrorPos(pos, n, last);
					}
				}
			}
			// 探索によりデータ長以内に、jisu個の解が見つからなかった
			return false;
		}

		/**
		 * Forney法で誤り訂正を行う
		 *		σ(z) = (1-α^i*z)(1-α^j*z)(1-α^k*z)
		 *		σ'(z) = α^i * (1-α^j*z)(1-α^k*z)...
		 *			   + α^j * (1-α^i*z)(1-α^k*z)...
		 *			   + α^k * (1-α^i*z)(1-α^j*z)...
		 *		ω(z) = (E^i/(1-α^i*z) + E^j/(1-α^j*z) + ...) * σ(z)
		 *			  = E^i*(1-α^j*z)(1-α^k*z)...
		 *			  + E^j*(1-α^i*z)(1-α^k*z)...
		 *			  + E^k*(1-α^i*z)(1-α^j*z)...
		 *		∴ E^i = α^i * ω(z) / σ'(z)
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 *		入力データ長さ
		 * @param jisu int
		 *		σの次数
		 * @param pos int[]
		 *		誤り位置配列
		 * @param sigma int[]
		 *		σ0,σ1,σ2, ... σ<jisu>
		 * @param omega int[]
		 *		ω0,ω1,ω2, ... ω<jisu-1>
		 */
		private void doForney(byte[] data, int length, int jisu, int[] pos, int[] sigma, int[] omega)
		{
			for (int i = 0; i < jisu; i++)
			{
				int ps = pos[i];
				int zlog = 255 - galois.toLog(ps);					// zのスカラー

				// ω(z)の計算
				int ov = omega[0];
				for (int j = 1; j < jisu; j++)
				{
					ov ^= galois.mulExp(omega[j], (zlog * j) % 255);		// ov += ωi * z^j
				}

				// σ'(z)の値を計算(σ(z)の形式的微分)
				int dv = sigma[1];
				for (int j = 2; j < jisu; j += 2)
				{
					dv ^= galois.mulExp(sigma[j + 1], (zlog * j) % 255);	// dv += σ<j+1> * z^j
				}

				/*
				 * 誤り訂正 E^i = α^i * ω(z) / σ'(z)
				 * 誤り位置の範囲はチェン探索のときに保証されているので、
				 * ここではチェックしない
				 */
				data[galois.toPos(length, ps)] ^= (byte)galois.mul(ps, galois.div(ov, dv));
			}
		}

		/**
		 * RSコードのデコード
		 *
		 * @param data int[]
		 *		入力データ配列
		 * @param length int
		 * 		パリティを含めたデータ長
		 * @param noCorrect boolean
		 * 		チェックのみで訂正は行わない
		 * @return bool
		 * 		0: エラーなし
		 * 		> 0: 戻り値個の誤りを訂正した
		 * 		< 0: 訂正不能
		 */
		public bool decode(byte[] data, int length, bool noCorrect, out int errors)
		{
			if (length < npar || length > 255)
				throw new Exception("RsDecode: wrong length");

			errors = 0;

			// シンドロームを計算
			int[] syn = new int[npar];
			if (galois.calcSyndrome(data, length, syn))
				return true; // エラー無し

			// シンドロームよりσとωを求める
			int[] sigma = new int[npar / 2 + 2];
			int[] omega = new int[npar / 2 + 1];
			int jisu = calcSigmaMBM(sigma, omega, syn);
			if (jisu <= 0)
				return false;
			
			// チェン探索により誤り位置を求める
			int[] pos = new int[jisu];
			if (!chienSearch(pos, length, jisu, sigma))
				return false;
			if (!noCorrect) // 誤り訂正
				doForney(data, length, jisu, pos, sigma, omega);
			errors = jisu;
			return true;
		}
	}
}
