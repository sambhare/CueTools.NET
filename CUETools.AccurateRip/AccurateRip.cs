using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Net;
using System.IO;
using CUETools.Codecs;
using CUETools.CDImage;

namespace CUETools.AccurateRip
{
	public class AccurateRipVerify : IAudioDest
	{
		public AccurateRipVerify(CDImageLayout toc)
		{
			_toc = toc;
			_accDisks = new List<AccDisk>();
			_crc32 = new Crc32();
			_hasLogCRC = false;
			_CRCLOG = new uint[_toc.AudioTracks + 1];
			for (int i = 0; i <= _toc.AudioTracks; i++)
				_CRCLOG[i] = 0;
			Init();
		}

		unsafe private void CalculateFrame450CRCs(int* samples, int count, int iTrack, int currentOffset)
		{
			int s1 = Math.Min(count, Math.Max(0, 450 * 588 - _arOffsetRange - currentOffset));
			int s2 = Math.Min(count, Math.Max(0, 451 * 588 + _arOffsetRange - currentOffset));
			if (s1 < s2)
				fixed (uint* FrameCRCs = &_offsetedFrame450CRC[iTrack, 0])
					for (int sj = s1; sj < s2; sj++)
					{
						int magicFrameOffset = (int)currentOffset + sj - 450 * 588 + 1;
						int firstOffset = Math.Max(-_arOffsetRange, magicFrameOffset - 588);
						int lastOffset = Math.Min(magicFrameOffset - 1, _arOffsetRange);
						uint sampleValue = (uint)((samples[2 * sj] & 0xffff) + (samples[2 * sj + 1] << 16));
						for (int oi = firstOffset; oi <= lastOffset; oi++)
							FrameCRCs[_arOffsetRange - oi] += sampleValue * (uint)(magicFrameOffset - oi);
					}
		}

		public uint Confidence(int iTrack)
		{
			if (ARStatus != null)
				return 0U;
			uint conf = 0;
			for (int di = 0; di < (int)AccDisks.Count; di++)
				if (CRC(iTrack) == AccDisks[di].tracks[iTrack].CRC)
					conf += AccDisks[di].tracks[iTrack].count;
			return conf;
		}

		public uint WorstTotal()
		{
			uint worstTotal = 0;
			for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
			{
				uint sumTotal = Total(iTrack);
				if (iTrack == 0 || worstTotal > sumTotal)
					worstTotal = sumTotal;
			}
			return worstTotal;
		}

		public uint WorstConfidence()
		{
			uint worstConfidence = 0;
			for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
			{
				uint sumConfidence = SumConfidence(iTrack);
				if (iTrack == 0 || worstConfidence > sumConfidence)
					worstConfidence = sumConfidence;
			}
			return worstConfidence;
		}

		public uint SumConfidence(int iTrack)
		{
			if (ARStatus != null)
				return 0U;
			uint conf = 0;
			for (int iDisk = 0; iDisk < AccDisks.Count; iDisk++)
				for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
					if (CRC(iTrack, oi) == AccDisks[iDisk].tracks[iTrack].CRC)
						conf += AccDisks[iDisk].tracks[iTrack].count;
			return conf;
		}

		public uint Confidence(int iTrack, int oi)
		{
			if (ARStatus != null)
				return 0U;
			uint conf = 0;
			for (int di = 0; di < (int)AccDisks.Count; di++)
				if (CRC(iTrack, oi) == AccDisks[di].tracks[iTrack].CRC)
					conf += AccDisks[di].tracks[iTrack].count;
			return conf;
		}

		public uint Total(int iTrack)
		{
			if (ARStatus != null)
				return 0U;
			uint total = 0;
			for (int di = 0; di < (int)AccDisks.Count; di++)
				total += AccDisks[di].tracks[iTrack].count;
			return total;
		}

		public uint DBCRC(int iTrack)
		{
			return ARStatus == null ? AccDisks[0].tracks[iTrack].CRC : 0U;
		}

		public uint BackupCRC(int iTrack)
		{
			return _backupCRC[iTrack];
		}

		public uint CRC(int iTrack)
		{
			return CRC(iTrack, 0);
		}

		public uint CRC(int iTrack, int oi)
		{
			if (oi == 0)
			{
				return
					((iTrack == _toc.AudioTracks - 1)
						? _offsetedCRCAR[iTrack + 1, 20 * 588 - 5 * 588]
						: _offsetedCRCAR[iTrack + 1, 20 * 588]) -
					((iTrack == 0)
						? _offsetedCRCAR[iTrack + 1, 5 * 588 - 1]
						: 0);
			}
			if (oi < 0)
			{
				uint crc = 0;
				if (iTrack > 0)
				{
					uint crcA = _offsetedCRCAR[iTrack, 20 * 588] - _offsetedCRCAR[iTrack, 20 * 588 + oi];
					uint sumA = _offsetedCRCSM[iTrack, 20 * 588] - _offsetedCRCSM[iTrack, 20 * 588 + oi];
					uint posA = _toc[iTrack + _toc.FirstAudio - 1].Length * 588 + (uint)oi;
					crc = crcA - sumA * posA;
				}
				uint crcB
					= ((iTrack == _toc.AudioTracks - 1)
						? _offsetedCRCAR[iTrack + 1, 20 * 588 - 5 * 588 + oi]
						: _offsetedCRCAR[iTrack + 1, 20 * 588 + oi])
					- ((iTrack == 0)
						? _offsetedCRCAR[iTrack + 1, 5 * 588 - 1 + oi]
						: 0);
				uint sumB
					= ((iTrack == _toc.AudioTracks - 1)
						? _offsetedCRCSM[iTrack + 1, 20 * 588 - 5 * 588 + oi]
						: _offsetedCRCSM[iTrack + 1, 20 * 588 + oi])
					- ((iTrack == 0)
						? _offsetedCRCSM[iTrack + 1, 5 * 588 - 1 + oi]
						: 0);
				uint posB = (uint)-oi;
				return crc + crcB + sumB * posB;
			} 
			else
			{
				uint crcA
					= ((iTrack == _toc.AudioTracks - 1)
						? _offsetedCRCAR[iTrack + 1, 20 * 588 - 5 * 588 + oi]
						: _offsetedCRCAR[iTrack + 1, 20 * 588])
					- ((iTrack == 0)
						? _offsetedCRCAR[iTrack + 1, 5 * 588 + oi - 1]
						: _offsetedCRCAR[iTrack + 1, oi]);
				uint sumA
					= ((iTrack == _toc.AudioTracks - 1)
						? _offsetedCRCSM[iTrack + 1, 20 * 588 - 5 * 588 + oi]
						: _offsetedCRCSM[iTrack + 1, 20 * 588])
					- ((iTrack == 0)
						? _offsetedCRCSM[iTrack + 1, 5 * 588 + oi - 1]
						: _offsetedCRCSM[iTrack + 1, oi]);
				uint posA = (uint)oi;
				uint crc = crcA - sumA * posA;
				if (iTrack < _toc.AudioTracks - 1)
				{
					uint crcB = _offsetedCRCAR[iTrack + 2, oi];
					uint sumB = _offsetedCRCSM[iTrack + 2, oi];
					uint posB = _toc[iTrack + _toc.FirstAudio].Length * 588 + (uint)-oi;
					crc += crcB + sumB * posB;
				}
				return crc;
			}
		}

		public uint CRC32(int iTrack)
		{
			return CRC32(iTrack, 0);
		}

		public uint CRC32(int iTrack, int oi)
		{
			if (_offsetedCRC32Res[iTrack, _arOffsetRange + oi] == 0)
			{
				uint crc = 0xffffffff;
				if (iTrack == 0)
				{
					for (iTrack = 0; iTrack <= _toc.AudioTracks; iTrack++)
					{
						int trackLength = (int)(iTrack > 0 ? _toc[iTrack + _toc.FirstAudio - 1].Length : _toc[_toc.FirstAudio].Pregap) * 588 * 4;
						if (oi < 0 && iTrack == 0)
							crc = _crc32.Combine(crc, 0, -oi * 4);
						if (trackLength == 0)
							continue;
						if (oi > 0 && (iTrack == 0 || (iTrack == 1 && _toc[_toc.FirstAudio].Pregap == 0)))
						{
							// Calculate track CRC skipping first oi samples by 'subtracting' their CRC
							crc = _crc32.Combine(_offsetedCRC32[iTrack, oi], _offsetedCRC32[iTrack, 20 * 588], trackLength - oi * 4);
							// Use 0xffffffff as an initial state
							crc = _crc32.Combine(0xffffffff, crc, trackLength - oi * 4);
						}
						else if (oi < 0 && iTrack == _toc.AudioTracks)
						{
							crc = _crc32.Combine(crc, _offsetedCRC32[iTrack, 20 * 588 + oi], trackLength + oi * 4);
						}
						else
						{
							crc = _crc32.Combine(crc, _offsetedCRC32[iTrack, 20 * 588], trackLength);
						}
						if (oi > 0 && iTrack == _toc.AudioTracks)
							crc = _crc32.Combine(crc, 0, oi * 4);
					}
					iTrack = 0;
				}
				else
				{
					int trackLength = (int)(iTrack > 0 ? _toc[iTrack + _toc.FirstAudio - 1].Length : _toc[_toc.FirstAudio].Pregap) * 588 * 4;
					if (oi > 0)
					{
						// Calculate track CRC skipping first oi samples by 'subtracting' their CRC
						crc = _crc32.Combine(_offsetedCRC32[iTrack, oi], _offsetedCRC32[iTrack, 20 * 588], trackLength - oi * 4);
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, crc, trackLength - oi * 4);
						// Add oi samples from next track CRC
						if (iTrack < _toc.AudioTracks)
							crc = _crc32.Combine(crc, _offsetedCRC32[iTrack + 1, oi], oi * 4);
						else
							crc = _crc32.Combine(crc, 0, oi * 4);
					}
					else if (oi < 0)
					{
						// Calculate CRC of previous track's last oi samples by 'subtracting' it's last CRCs
						crc = _crc32.Combine(_offsetedCRC32[iTrack - 1, 20 * 588 + oi], _offsetedCRC32[iTrack - 1, 20 * 588], -oi * 4);
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, crc, -oi * 4);
						// Add this track's CRC without last oi samples
						crc = _crc32.Combine(crc, _offsetedCRC32[iTrack, 20 * 588 + oi], trackLength + oi * 4);
					}
					else // oi == 0
					{
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, _offsetedCRC32[iTrack, 20 * 588], trackLength);
					}
				}
				_offsetedCRC32Res[iTrack, _arOffsetRange + oi] = crc ^ 0xffffffff;
			}
			return _offsetedCRC32Res[iTrack, _arOffsetRange + oi];
		}

		public uint CRCWONULL(int iTrack)
		{
			return CRCWONULL(iTrack, 0);
		}

		public uint CRCWONULL(int iTrack, int oi)
		{
			if (_offsetedCRCWNRes[iTrack, _arOffsetRange + oi] == 0)
			{
				uint crc = 0xffffffff;
				if (iTrack == 0)
				{
					for (iTrack = 0; iTrack <= _toc.AudioTracks; iTrack++)
					{
						int trackLength = (int)(iTrack > 0 ? _toc[iTrack + _toc.FirstAudio - 1].Length : _toc[_toc.FirstAudio].Pregap) * 588 * 4
							- _offsetedCRCNulls[iTrack, 20 * 588] * 2;
						crc = _crc32.Combine(crc, _offsetedCRCWN[iTrack, 20 * 588], trackLength);
					}
					iTrack = 0;
				}
				else
				{
					int trackLength = (int)(iTrack > 0 ? _toc[iTrack + _toc.FirstAudio - 1].Length : _toc[_toc.FirstAudio].Pregap) * 588 * 4;
					if (oi > 0)
					{
						int nonzeroPrevLength = trackLength - oi * 4 -
							(_offsetedCRCNulls[iTrack, 20 * 588] - _offsetedCRCNulls[iTrack, oi]) * 2;
						// Calculate track CRC skipping first oi samples by 'subtracting' their CRC
						crc = _crc32.Combine(
							_offsetedCRCWN[iTrack, oi],
							_offsetedCRCWN[iTrack, 20 * 588],
							nonzeroPrevLength);
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, crc, nonzeroPrevLength);
						// Add oi samples from next track CRC
						if (iTrack < _toc.AudioTracks)
							crc = _crc32.Combine(crc,
								_offsetedCRCWN[iTrack + 1, oi],
								oi * 4 - _offsetedCRCNulls[iTrack + 1, oi] * 2);
					}
					else if (oi < 0)
					{
						int nonzeroPrevLength = -oi * 4 -
							(_offsetedCRCNulls[iTrack - 1, 20 * 588] - _offsetedCRCNulls[iTrack - 1, 20 * 588 + oi]) * 2;
						// Calculate CRC of previous track's last oi samples by 'subtracting' it's last CRCs
						crc = _crc32.Combine(
							_offsetedCRCWN[iTrack - 1, 20 * 588 + oi],
							_offsetedCRCWN[iTrack - 1, 20 * 588],
							nonzeroPrevLength);
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, crc, nonzeroPrevLength);
						// Add this track's CRC without last oi samples
						crc = _crc32.Combine(crc,
							_offsetedCRCWN[iTrack, 20 * 588 + oi],
							trackLength + oi * 4 - _offsetedCRCNulls[iTrack, 20 * 588 + oi] * 2);
					}
					else // oi == 0
					{
						// Use 0xffffffff as an initial state
						crc = _crc32.Combine(0xffffffff, _offsetedCRCWN[iTrack, 20 * 588], trackLength - _offsetedCRCNulls[iTrack, 20 * 588] * 2);
					}
				}
				_offsetedCRCWNRes[iTrack, _arOffsetRange + oi] = crc ^ 0xffffffff;
			}
			return _offsetedCRCWNRes[iTrack, _arOffsetRange + oi];
		}

		public uint CRCLOG(int iTrack)
		{
			return _CRCLOG[iTrack];
		}

		public void CRCLOG(int iTrack, uint value)
		{
			_hasLogCRC = true;
			_CRCLOG[iTrack] = value;
		}

		public uint CRC450(int iTrack, int oi)
		{
			return _offsetedFrame450CRC[iTrack, _arOffsetRange - oi];
		}

		public unsafe void CalculateCRCs(int* pSampleBuff, int count, int currentOffset, int offs)
		{
			uint crcar = _offsetedCRCAR[_currentTrack, 20 * 588];
			uint crcsm = _offsetedCRCSM[_currentTrack, 20 * 588];
			uint crc = _offsetedCRC32[_currentTrack, 20 * 588];
			uint crcwn = _offsetedCRCWN[_currentTrack, 20 * 588];
			int crcnulls = _offsetedCRCNulls[_currentTrack, 20 * 588];
			fixed (uint* t = _crc32.table)
			{
				for (int i = 0; i < count; i++)
				{
					_offsetedCRCAR[_currentTrack, offs + i] = crcar;
					_offsetedCRCSM[_currentTrack, offs + i] = crcsm;
					_offsetedCRC32[_currentTrack, offs + i] = crc;
					_offsetedCRCWN[_currentTrack, offs + i] = crcwn;
					_offsetedCRCNulls[_currentTrack, offs + i] = crcnulls;

					uint lo = (uint)*(pSampleBuff++);
					crc = (crc >> 8) ^ t[(byte)(crc ^ lo)];
					crc = (crc >> 8) ^ t[(byte)(crc ^ (lo >> 8))];
					if (lo != 0)
					{
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ lo)];
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ (lo >> 8))];
					}
					else crcnulls++;

					uint hi = (uint)*(pSampleBuff++);
					crc = (crc >> 8) ^ t[(byte)(crc ^ hi)];
					crc = (crc >> 8) ^ t[(byte)(crc ^ (hi >> 8))];
					if (hi != 0)
					{
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ hi)];
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ (hi >> 8))];
					}
					else crcnulls++;

					uint sampleValue = (lo & 0xffff) + (hi << 16);
					crcsm += sampleValue;
					crcar += sampleValue * (uint)(currentOffset + i + 1);
				}
			}
			//_offsetedCRCAR[_currentTrack, offs + count] = crcar;
			//_offsetedCRCSM[_currentTrack, offs + count] = crcsm;

			_offsetedCRCAR[_currentTrack, 20 * 588] = crcar;
			_offsetedCRCSM[_currentTrack, 20 * 588] = crcsm;
			_offsetedCRC32[_currentTrack, 20 * 588] = crc;
			_offsetedCRCWN[_currentTrack, 20 * 588] = crcwn;
			_offsetedCRCNulls[_currentTrack, 20 * 588] = crcnulls;
		}

		public unsafe void CalculateCRCs(int* pSampleBuff, int count, int currentOffset)
		{
			uint crcar = _offsetedCRCAR[_currentTrack, 20 * 588];
			uint crcsm = _offsetedCRCSM[_currentTrack, 20 * 588];
			uint crc = _offsetedCRC32[_currentTrack, 20 * 588];
			uint crcwn = _offsetedCRCWN[_currentTrack, 20 * 588];
			int crcnulls = _offsetedCRCNulls[_currentTrack, 20 * 588];
			fixed (uint* t = _crc32.table)
			{
				for (int i = 0; i < count; i++)
				{
					uint lo = (uint)*(pSampleBuff++);
					crc = (crc >> 8) ^ t[(byte)(crc ^ lo)];
					crc = (crc >> 8) ^ t[(byte)(crc ^ (lo >> 8))];
					if (lo != 0)
					{
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ lo)];
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ (lo >> 8))];
					}
					else crcnulls++;

					uint hi = (uint)*(pSampleBuff++);
					crc = (crc >> 8) ^ t[(byte)(crc ^ hi)];
					crc = (crc >> 8) ^ t[(byte)(crc ^ (hi >> 8))];
					if (hi != 0)
					{
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ hi)];
						crcwn = (crcwn >> 8) ^ t[(byte)(crcwn ^ (hi >> 8))];
					}
					else crcnulls++;

					uint sampleValue = (lo & 0xffff) + (hi << 16);
					crcsm += sampleValue;
					crcar += sampleValue * (uint)(currentOffset + i + 1);
				}
			}
			_offsetedCRCAR[_currentTrack, 20 * 588] = crcar;
			_offsetedCRCSM[_currentTrack, 20 * 588] = crcsm;
			_offsetedCRC32[_currentTrack, 20 * 588] = crc;
			_offsetedCRCWN[_currentTrack, 20 * 588] = crcwn;
			_offsetedCRCNulls[_currentTrack, 20 * 588] = crcnulls;
		}

		public void Write(AudioBuffer sampleBuffer)
		{
			sampleBuffer.Prepare(this);

			int pos = 0;
			while (pos < sampleBuffer.Length)
			{
				// Process no more than there is in the buffer, no more than there is in this track, and no more than up to a sector boundary.
				int copyCount = Math.Min(Math.Min(sampleBuffer.Length - pos, (int)_samplesRemTrack), 588 - (int)_sampleCount % 588);
				// Calculate offset within a track
				int currentOffset = (int)_sampleCount - (int)(_currentTrack > 0 ? _toc[_currentTrack + _toc.FirstAudio - 1].Start * 588 : 0);
				int currentSector = currentOffset / 588;
				int remaingSectors = (int)(_samplesRemTrack - 1) / 588;

				unsafe
				{
					fixed (int* pSampleBuff = &sampleBuffer.Samples[pos, 0])
					//fixed (byte* pByteBuff = &sampleBuffer.Bytes[pos * sampleBuffer.BlockAlign])
					{
						if (currentSector < 5 || (_currentTrack == 1 && currentSector < 10))
							CalculateCRCs(pSampleBuff, copyCount, currentOffset, currentOffset);
						else if (remaingSectors < 5 || (_currentTrack == _toc.AudioTracks && remaingSectors < 10))
							CalculateCRCs(pSampleBuff, copyCount, currentOffset, 20 * 588 - (int)_samplesRemTrack);
						else
							CalculateCRCs(pSampleBuff, copyCount, currentOffset);

						if (currentSector >= 440 && currentSector <= 460)
							CalculateFrame450CRCs(pSampleBuff, copyCount, _currentTrack - 1, currentOffset);
					}
				}
				pos += copyCount;
				_samplesRemTrack -= copyCount;
				_sampleCount += copyCount;
				CheckPosition();
			}
		}

		public void Init()
		{
			_offsetedCRCAR = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRCSM = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRC32 = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRC32Res = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRCWN = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRCWNRes = new uint[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedCRCNulls = new int[_toc.AudioTracks + 1, 20 * 588 + 1];
			_offsetedFrame450CRC = new uint[_toc.AudioTracks, 20 * 588];
			_currentTrack = 0;
			_sampleCount = _toc[_toc.FirstAudio][0].Start * 588;
			_samplesRemTrack = _toc[_toc.FirstAudio].Pregap * 588;
			CheckPosition();
		}

		public void CreateBackup(int writeOffset)
		{
			_backupCRC = new uint[_toc.AudioTracks];
			for (int i = 0; i < _toc.AudioTracks; i++)
				_backupCRC[i] = CRC(i, writeOffset);
		}

		private void CheckPosition()
		{
			while (_samplesRemTrack <= 0)
			{
				if (++_currentTrack > _toc.AudioTracks)
					return;
				_samplesRemTrack = _toc[_currentTrack + _toc.FirstAudio - 1].Length * 588;
			}
		}

		private uint readIntLE(byte[] data, int pos)
		{
			return (uint)(data[pos] + (data[pos + 1] << 8) + (data[pos + 2] << 16) + (data[pos + 3] << 24));
		}

		public void ContactAccurateRip(string accurateRipId)
		{
			// Calculate the three disc ids used by AR
			uint discId1 = 0;
			uint discId2 = 0;
			uint cddbDiscId = 0;

			string[] n = accurateRipId.Split('-');
			if (n.Length != 3)
			{
				throw new Exception("Invalid accurateRipId.");
			}
			discId1 = UInt32.Parse(n[0], NumberStyles.HexNumber);
			discId2 = UInt32.Parse(n[1], NumberStyles.HexNumber);
			cddbDiscId = UInt32.Parse(n[2], NumberStyles.HexNumber);

			string url = String.Format("http://www.accuraterip.com/accuraterip/{0:x}/{1:x}/{2:x}/dBAR-{3:d3}-{4:x8}-{5:x8}-{6:x8}.bin",
				discId1 & 0xF, discId1 >> 4 & 0xF, discId1 >> 8 & 0xF, _toc.AudioTracks, discId1, discId2, cddbDiscId);

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.Method = "GET";

			try
			{
				HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
				_accResult = resp.StatusCode;

				if (_accResult == HttpStatusCode.OK)
				{
					// Retrieve response stream and wrap in StreamReader
					Stream respStream = resp.GetResponseStream();

					// Allocate byte buffer to hold stream contents
					byte[] urlData = new byte[13];
					int urlDataLen, bytesRead;

					_accDisks.Clear();
					while (true)
					{
						for (urlDataLen = 0; urlDataLen < 13; urlDataLen += bytesRead)
						{
							bytesRead = respStream.Read(urlData, urlDataLen, 13 - urlDataLen);
							if (0 == bytesRead)
								break;
						}
						if (urlDataLen == 0)
							break;
						if (urlDataLen < 13)
						{
							_accResult = HttpStatusCode.PartialContent;
							return;
						}
						AccDisk dsk = new AccDisk();
						dsk.count = urlData[0];
						dsk.discId1 = readIntLE(urlData, 1);
						dsk.discId2 = readIntLE(urlData, 5);
						dsk.cddbDiscId = readIntLE(urlData, 9);

						for (int i = 0; i < dsk.count; i++)
						{
							for (urlDataLen = 0; urlDataLen < 9; urlDataLen += bytesRead)
							{
								bytesRead = respStream.Read(urlData, urlDataLen, 9 - urlDataLen);
								if (0 == bytesRead)
								{
									_accResult = HttpStatusCode.PartialContent;
									return;
								}
							}
							AccTrack trk = new AccTrack();
							trk.count = urlData[0];
							trk.CRC = readIntLE(urlData, 1);
							trk.Frame450CRC = readIntLE(urlData, 5);
							dsk.tracks.Add(trk);
						}
						_accDisks.Add(dsk);
					}
					respStream.Close();
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError)
					_accResult = ((HttpWebResponse)ex.Response).StatusCode;
				else
					_accResult = HttpStatusCode.BadRequest;
			}
		}

		public void Close()
		{
			if (_sampleCount != _finalSampleCount)
				throw new Exception("_sampleCount != _finalSampleCount");
		}

		public void Delete()
		{
			throw new Exception("unsupported");
		}

		public int CompressionLevel
		{
			get { return 0; }
			set { }
		}

		public string Options
		{
			set
			{
				if (value == null || value == "") return;
				throw new Exception("Unsupported options " + value);
			}
		}

		public AudioPCMConfig PCM
		{
			get { return AudioPCMConfig.RedBook; }
		}

		public long FinalSampleCount
		{
			set
			{
				if (value < 0) // != _toc.Length?
					throw new Exception("invalid FinalSampleCount");
				_finalSampleCount = value;
			}
		}

		public long BlockSize
		{
			set { throw new Exception("unsupported"); }
		}

		public string Path
		{
			get { throw new Exception("unsupported"); }
		}

		public void GenerateLog(TextWriter sw, int oi)
		{
			for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
			{
				uint count = 0;
				uint partials = 0;
				uint conf = 0;
				for (int di = 0; di < (int)AccDisks.Count; di++)
				{
					count += AccDisks[di].tracks[iTrack].count;
					if (CRC(iTrack, oi) == AccDisks[di].tracks[iTrack].CRC)
						conf += AccDisks[di].tracks[iTrack].count;
					if (CRC450(iTrack, oi) == AccDisks[di].tracks[iTrack].Frame450CRC)
						partials += AccDisks[di].tracks[iTrack].count;
				}
				if (conf > 0)
					sw.WriteLine(String.Format(" {0:00}\t[{1:x8}] ({3:00}/{2:00}) Accurately ripped", iTrack + 1, CRC(iTrack, oi), count, conf));
				else if (partials > 0)
					sw.WriteLine(String.Format(" {0:00}\t[{1:x8}] ({3:00}/{2:00}) Partial match", iTrack + 1, CRC(iTrack, oi), count, partials));
				else
					sw.WriteLine(String.Format(" {0:00}\t[{1:x8}] (00/{2:00}) No matches", iTrack + 1, CRC(iTrack, oi), count));
			}
		}

		public void GenerateFullLog(TextWriter sw, bool verbose)
		{
			if (AccResult == HttpStatusCode.NotFound)
			{
				sw.WriteLine("Disk not present in database.");
				//for (iTrack = 0; iTrack < TrackCount; iTrack++)
				//    sw.WriteLine(String.Format(" {0:00}\t[{1:x8}] Disk not present in database", iTrack + 1, _tracks[iTrack].CRC));
			}
			else if (AccResult != HttpStatusCode.OK)
			{
				sw.WriteLine("Database access error: " + AccResult.ToString());
				//for (iTrack = 0; iTrack < TrackCount; iTrack++)
				//    sw.WriteLine(String.Format(" {0:00}\t[{1:x8}] Database access error {2}", iTrack + 1, _tracks[iTrack].CRC, accResult.ToString()));
			}
			else
			{
				if (verbose)
				{
					sw.WriteLine("Track\t[ CRC    ] Status");
					GenerateLog(sw, 0);
					uint offsets_match = 0;
					for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
					{
						uint matches = 0;
						for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
							for (int di = 0; di < (int)AccDisks.Count; di++)
								if ((CRC(iTrack, oi) == AccDisks[di].tracks[iTrack].CRC && AccDisks[di].tracks[iTrack].CRC != 0))
								{
									matches++;
									break;
								}
						if (matches == _toc.AudioTracks && oi != 0)
						{
							if (offsets_match++ > 16)
							{
								sw.WriteLine("More than 16 offsets match!");
								break;
							}
							sw.WriteLine("Offsetted by {0}:", oi);
							GenerateLog(sw, oi);
						}
					}
					offsets_match = 0;
					for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
					{
						uint matches = 0, partials = 0;
						for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
							for (int di = 0; di < (int)AccDisks.Count; di++)
							{
								if ((CRC(iTrack, oi) == AccDisks[di].tracks[iTrack].CRC && AccDisks[di].tracks[iTrack].CRC != 0))
								{
									matches++;
									break;
								}
								if ((CRC450(iTrack, oi) == AccDisks[di].tracks[iTrack].Frame450CRC && AccDisks[di].tracks[iTrack].Frame450CRC != 0))
									partials++;
							}
						if (matches != _toc.AudioTracks && oi != 0 && matches + partials != 0)
						{
							if (offsets_match++ > 16)
							{
								sw.WriteLine("More than 16 offsets match!");
								break;
							}
							sw.WriteLine("Offsetted by {0}:", oi);
							GenerateLog(sw, oi);
						}
					}
				}
				else
				{
					sw.WriteLine("Track\t Status");
					for (int iTrack = 0; iTrack < _toc.AudioTracks; iTrack++)
					{
						uint total = Total(iTrack);
						uint conf = 0;
						bool zeroOffset = false;
						StringBuilder pressings = new StringBuilder();
						for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
							for (int iDisk = 0; iDisk < AccDisks.Count; iDisk++)
							{
								if (CRC(iTrack, oi) == AccDisks[iDisk].tracks[iTrack].CRC && (AccDisks[iDisk].tracks[iTrack].CRC != 0 || oi == 0))
								{
									conf += AccDisks[iDisk].tracks[iTrack].count;
									if (oi == 0)
										zeroOffset = true;
									pressings.AppendFormat("{0}{1}({2})", pressings.Length > 0 ? "," : "", oi, AccDisks[iDisk].tracks[iTrack].count);
								}
							}
						if (conf > 0 && zeroOffset && pressings.Length == 0)
							sw.WriteLine(String.Format(" {0:00}\t ({2:00}/{1:00}) Accurately ripped", iTrack + 1, total, conf));
						else if (conf > 0 && zeroOffset)
							sw.WriteLine(String.Format(" {0:00}\t ({2:00}/{1:00}) Accurately ripped, all offset(s) {3}", iTrack + 1, total, conf, pressings));
						else if (conf > 0)
							sw.WriteLine(String.Format(" {0:00}\t ({2:00}/{1:00}) Accurately ripped with offset(s) {3}", iTrack + 1, total, conf, pressings));
						else if (total > 0)
							sw.WriteLine(String.Format(" {0:00}\t (00/{1:00}) NOT ACCURATE", iTrack + 1, total));
						else
							sw.WriteLine(String.Format(" {0:00}\t (00/00) Track not present in database", iTrack + 1));
					}
				}
			}
			if (CRC32(0) != 0 && (_hasLogCRC || verbose))
			{
				sw.WriteLine("");
				sw.WriteLine("Track\t[ CRC32  ]\t[W/O NULL]\t{0:10}", _hasLogCRC ? "[  LOG   ]" : "");
				for (int iTrack = 0; iTrack <= _toc.AudioTracks; iTrack++)
				{
					string inLog, extra = "";
					if (CRCLOG(iTrack) == 0)
						inLog = "";
					else if (CRCLOG(iTrack) == CRC32(iTrack))
						inLog = "  CRC32   ";
					else if (CRCLOG(iTrack) == CRCWONULL(iTrack))
						inLog = " W/O NULL ";
					else
					{
						inLog = String.Format("[{0:X8}]", CRCLOG(iTrack));
						for (int jTrack = 1; jTrack <= _toc.AudioTracks; jTrack++)
						{
							if (CRCLOG(iTrack) == CRC32(jTrack))
							{
								extra = string.Format(": CRC32 for track {0}", jTrack);
								break;
							}
							if (CRCLOG(iTrack) == CRCWONULL(jTrack))
							{
								extra = string.Format(": W/O NULL for track {0}", jTrack);
								break;
							}
						}
						if (extra == "")
							for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
								if (CRCLOG(iTrack) == CRC32(iTrack, oi))
								{
									inLog = "  CRC32   ";
									extra = string.Format(": offset {0}", oi);
									break;
								}
						if (extra == "")
							for (int oi = -_arOffsetRange; oi <= _arOffsetRange; oi++)
								if (CRCLOG(iTrack) == CRCWONULL(iTrack, oi))
								{
									inLog = " W/O NULL ";
									if (extra == "")
										extra = string.Format(": offset {0}", oi);
									else
									{
										extra = string.Format(": with offset");
										break;
									}
								}
					}
					sw.WriteLine(String.Format(" {0}\t[{1:X8}]\t[{2:X8}]\t{3:10}{4}", iTrack == 0 ? "--" : string.Format("{0:00}", iTrack), CRC32(iTrack), CRCWONULL(iTrack), inLog, extra));
				}
			}
		}

		private static uint sumDigits(uint n)
		{
			uint r = 0;
			while (n > 0)
			{
				r = r + (n % 10);
				n = n / 10;
			}
			return r;
		}

		static string CachePath
		{
			get
			{
				string cache = System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CUE Tools"), "AccurateRipCache");
				if (!Directory.Exists(cache))
					Directory.CreateDirectory(cache);
				return cache;
			}
		}

		public static bool FindDriveReadOffset(string driveName, out int driveReadOffset)
		{
			string fileName = System.IO.Path.Combine(CachePath, "DriveOffsets.bin");
			if (!File.Exists(fileName))
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://www.accuraterip.com/accuraterip/DriveOffsets.bin");
				req.Method = "GET";
				try
				{
					HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
					if (resp.StatusCode != HttpStatusCode.OK)
					{
						driveReadOffset = 0;
						return false;
					}
					Stream respStream = resp.GetResponseStream();
					FileStream myOffsetsSaved = new FileStream(fileName, FileMode.CreateNew, FileAccess.Write);
					byte[] buff = new byte[0x8000];
					do
					{
						int count = respStream.Read(buff, 0, buff.Length);
						if (count == 0) break;
						myOffsetsSaved.Write(buff, 0, count);
					} while (true);
					respStream.Close();
					myOffsetsSaved.Close();
				}
				catch (WebException ex)
				{
					driveReadOffset = 0;
					return false;
				}
			}
			FileStream myOffsets = new FileStream(fileName, FileMode.Open, FileAccess.Read);
			BinaryReader offsetReader = new BinaryReader(myOffsets);
			do
			{
				short readOffset = offsetReader.ReadInt16();
				byte[] name = offsetReader.ReadBytes(0x21);
				byte[] misc = offsetReader.ReadBytes(0x22);
				int len = name.Length;
				while (len > 0 && name[len - 1] == '\0') len--;
				string strname = Encoding.ASCII.GetString(name, 0, len);
				if (strname == driveName)
				{
					driveReadOffset = readOffset;
					return true;
				}
			} while (myOffsets.Position + 0x45 <= myOffsets.Length);
			offsetReader.Close();
			driveReadOffset = 0;
			return false;
		}

		public static string CalculateCDDBQuery(CDImageLayout toc)
		{
			StringBuilder query = new StringBuilder(CalculateCDDBId(toc));
			query.AppendFormat("+{0}", toc.TrackCount);
			for (int iTrack = 1; iTrack <= toc.TrackCount; iTrack++)
				query.AppendFormat("+{0}", toc[iTrack].Start + 150);
			query.AppendFormat("+{0}", toc.Length / 75 - toc[1].Start / 75);
			return query.ToString();
		}

		public static string CalculateCDDBId(CDImageLayout toc)
		{
			uint cddbDiscId = 0;
			for (int iTrack = 1; iTrack <= toc.TrackCount; iTrack++)
				cddbDiscId += sumDigits(toc[iTrack].Start / 75 + 2); // !!!!!!!!!!!!!!!!! %255 !!
			return string.Format("{0:X8}", (((cddbDiscId % 255) << 24) + ((toc.Length / 75 - toc[1].Start / 75) << 8) + (uint)toc.TrackCount) & 0xFFFFFFFF);
		}

		public static string CalculateAccurateRipId(CDImageLayout toc)
		{
			// Calculate the three disc ids used by AR
			uint discId1 = 0;
			uint discId2 = 0;
			uint num = 0;

			for (int iTrack = 1; iTrack <= toc.TrackCount; iTrack++)
				if (toc[iTrack].IsAudio)
				{
					discId1 += toc[iTrack].Start;
					discId2 += Math.Max(toc[iTrack].Start, 1) * (++num);
				}
			discId1 += toc.Length;
			discId2 += Math.Max(toc.Length, 1) * (++num);
			discId1 &= 0xFFFFFFFF;
			discId2 &= 0xFFFFFFFF;
			return string.Format("{0:x8}-{1:x8}-{2}", discId1, discId2, CalculateCDDBId(toc).ToLower());
		}

		public List<AccDisk> AccDisks
		{
			get
			{
				return _accDisks;
			}
		}

		public HttpStatusCode AccResult
		{
			get
			{
				return _accResult;
			}
		}

		public string ARStatus
		{
			get
			{
				return _accResult == HttpStatusCode.NotFound ? "disk not present in database" :
					_accResult == HttpStatusCode.OK ? null
					: _accResult.ToString();
			}
		}

		CDImageLayout _toc;
		long _sampleCount, _finalSampleCount, _samplesRemTrack;
		int _currentTrack;
		private List<AccDisk> _accDisks;
		private HttpStatusCode _accResult;
		private uint[,] _offsetedCRCAR;
		private uint[,] _offsetedCRCSM;
		private uint[,] _offsetedCRC32;
		private uint[,] _offsetedCRCWN;
		private uint[,] _offsetedCRCWNRes;
		private int[,] _offsetedCRCNulls;
		private uint[,] _offsetedCRC32Res;
		private uint[,] _offsetedFrame450CRC;
		private uint[] _CRCLOG;
		private uint[] _backupCRC;

		Crc32 _crc32;

		private bool _hasLogCRC;

		private const int _arOffsetRange = 5 * 588 - 1;
	}

	public struct AccTrack
	{
		public uint count;
		public uint CRC;
		public uint Frame450CRC;
	}

	public class AccDisk
	{
		public uint count;
		public uint discId1;
		public uint discId2;
		public uint cddbDiscId;
		public List<AccTrack> tracks;

		public AccDisk()
		{
			tracks = new List<AccTrack>();
		}
	}
}
