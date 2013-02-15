﻿using Agilent.MassSpectrometry.DataAnalysis;
using Agilent.MassSpectrometry.DataAnalysis.Utilities;
using CSMSL.IO;
using CSMSL.Spectral;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CSMSL.IO.Agilent
{
    public class AgilentDDirectory : MsDataFile
    {
        private IMsdrDataReader _msdr;

        public AgilentDDirectory(string directoryPath, bool openImmediately = false)
            : base(directoryPath, MsDataFileType.AgilentRawFile, openImmediately) { }       
          
        public override void Open()
        {
            if(!IsOpen)
            {
                _msdr = (IMsdrDataReader)new MassSpecDataReader();
                _msdr.OpenDataFile(FilePath);
                base.Open();
            }
        }

        public override void Dispose()
        {
            if(_msdr != null)
            {
                _msdr.CloseDataFile();
                _msdr = null;
            }
            base.Dispose();
        }

        protected override int GetFirstSpectrumNumber()
        {
            return 1;
        }

        protected override int GetLastSpectrumNumber()
        {
            return (int)(_msdr.MSScanFileInformation.TotalScansPresent);
        }

        public override double GetRetentionTime(int spectrumNumber)
        {
            IMSScanRecord scan_record = _msdr.GetScanRecord(spectrumNumber - 1);
            return scan_record.RetentionTime;
        }

        public override int GetMsnOrder(int spectrumNumber)
        {
            IMSScanRecord scan_record = _msdr.GetScanRecord(spectrumNumber - 1);
            return scan_record.MSLevel == MSLevel.MSMS ? 2 : 1;
        }

        private object GetExtraValue(int spectrumNumber, string filter)
        {
            IBDAActualData[] actuals = _msdr.ActualsInformation.GetActualCollection(GetRetentionTime(spectrumNumber));
            foreach(IBDAActualData actual in actuals)
            {
                if(actual.DisplayName == filter)
                {
                    return actual.DisplayValue;
                }
            }
            return null;
        }

        public override Polarity GetPolarity(int spectrumNumber)
        {
            IMSScanRecord scan_record = _msdr.GetScanRecord(spectrumNumber - 1);
            switch(scan_record.IonPolarity)
            {
                case IonPolarity.Positive:
                    return Polarity.Positive;
                case IonPolarity.Negative:
                    return Polarity.Negative;
                default:
                    return Polarity.Neutral;
            }
        }

        public override Spectral.MZSpectrum GetMzSpectrum(int spectrumNumber)
        {
            IBDASpecData spectrum = _msdr.GetSpectrum(spectrumNumber - 1);
            return new MZSpectrum(spectrum.XArray, spectrum.YArray);
        }

        public override MzAnalyzerType GetMzAnalyzer(int spectrumNumber)
        {
            IBDASpecData spectrum = _msdr.GetSpectrum(spectrumNumber - 1);
            switch(spectrum.DeviceType)
            {
                case DeviceType.IonTrap:
                    return MzAnalyzerType.IonTrap3D;
                case DeviceType.Quadrupole:
                case DeviceType.TandemQuadrupole:
                    return MzAnalyzerType.Quadrupole;
                case DeviceType.QuadrupoleTimeOfFlight:
                case DeviceType.TimeOfFlight:
                    return MzAnalyzerType.TOF;
                default:
                    return MzAnalyzerType.Unknown;
            }
        }

        public override double GetPrecusorMz(int spectrumNumber, int msnOrder = 2)
        {
            IMSScanRecord scan_record = _msdr.GetScanRecord(spectrumNumber - 1);
            return scan_record.MZOfInterest;
        }

        private static Regex ISOLATION_WIDTH_REGEX;

        public override double GetIsolationWidth(int spectrumNumber, int msnOrder = 2)
        {
            string acquisition_method;
            using(StreamReader acquisition_method_sr = new StreamReader(Path.Combine(_msdr.FileInformation.DataFileName, @"AcqData\AcqMethod.xml")))
            {
                acquisition_method = acquisition_method_sr.ReadToEnd();
            }
            if(ISOLATION_WIDTH_REGEX == null)
            {
                ISOLATION_WIDTH_REGEX = new Regex(@"\s*(?:&lt;|<)ID(?:&gt;|>)TargetIsolationWidth(?:&lt;|<)/ID(?:&gt;|>)\s*(?:&lt;|<)Value(?:&gt;|>).*\(~([0-9.])+ amu\)(?:&lt;|<)/Value(?:&gt;|>)");
            }
            Match match = ISOLATION_WIDTH_REGEX.Match(acquisition_method);
            return double.Parse(match.Groups[1].Value);
        }

        public override Proteomics.DissociationType GetDissociationType(int spectrumNumber, int msnOrder = 2)
        {
            return Proteomics.DissociationType.CID;
        }

        public override Range GetMzRange(int spectrumNumber)
        {
            IBDASpecData spectrum = _msdr.GetSpectrum(spectrumNumber - 1);
            return new Range(spectrum.MeasuredMassRange.Start, spectrum.MeasuredMassRange.End);
        }

        public override short GetPrecusorCharge(int spectrumNumber, int msnOrder = 2)
        {
            IBDASpecData spectrum = _msdr.GetSpectrum(spectrumNumber - 1);
            int precursor_charge;
            spectrum.GetPrecursorCharge(out precursor_charge);
            return (short)precursor_charge;
        }

        public override int GetSpectrumNumber(double retentionTime)
        {
            IBDAChromData tic = _msdr.GetTIC();
            int index = -1;
            for(int i = 0; i < tic.TotalDataPoints; i++)
            {
                if(index < 0 || Math.Abs(tic.XArray[i] - retentionTime) < Math.Abs(tic.XArray[index] - retentionTime))
                {
                    index = i;
                }
            }
            return index + 1;
        }

        public override double GetInjectionTime(int spectrumNumber)
        {
            IMSScanRecord scan_record = _msdr.GetScanRecord(spectrumNumber - 1);
            int num_transients = 0;
            double length_transient = double.NaN;
            IBDAActualData[] actuals = _msdr.ActualsInformation.GetActualCollection(scan_record.RetentionTime);
            foreach(IBDAActualData actual in actuals)
            {
                if(actual.DisplayName == "Number of Transients")
                {
                    num_transients = int.Parse(actual.DisplayValue);
                }
                else if(actual.DisplayName == "Length of Transients")
                {
                    length_transient = double.Parse(actual.DisplayValue);
                }
            }
            return num_transients * length_transient;  // may be off by a factor of two for extended dynamic range mode
        }
    }
}