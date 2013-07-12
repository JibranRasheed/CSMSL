﻿///////////////////////////////////////////////////////////////////////////
//  AminoAcidPolymer.cs - A linear sequence of amino acid residues        /
//                                                                        /
//  Copyright 2012 Derek J. Bailey                                        /
//  This file is part of CSMSL.                                           /
//                                                                        /
//  CSMSL is free software: you can redistribute it and/or modify         /
//  it under the terms of the GNU General Public License as published by  /
//  the Free Software Foundation, either version 3 of the License, or     /
//  (at your option) any later version.                                   /
//                                                                        /
//  CSMSL is distributed in the hope that it will be useful,              /
//  but WITHOUT ANY WARRANTY; without even the implied warranty of        /
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         /
//  GNU General Public License for more details.                          /
//                                                                        /
//  You should have received a copy of the GNU General Public License     /
//  along with CSMSL.  If not, see <http://www.gnu.org/licenses/>.        /
///////////////////////////////////////////////////////////////////////////

using CSMSL.Chemistry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSMSL.Proteomics
{

    /// <summary>
    /// A linear polymer of amino acids
    /// </summary>
    public abstract class AminoAcidPolymer : IEquatable<AminoAcidPolymer>, IMass, IAminoAcidSequence
    {
        /// <summary>
        /// The default chemical formula of the C terminus (hydroxyl group)
        /// </summary>
        public static readonly ChemicalFormula DefaultCTerminus = new ChemicalFormula("OH");

        /// <summary>
        /// The default chemical formula of the N terminus (hydrogen)
        /// </summary>
        public static readonly ChemicalFormula DefaultNTerminus = new ChemicalFormula("H");

        /// <summary>
        /// The regex for peptide sequences with the possibilities of modifications (either chemical formulas or masses)
        /// </summary>
        //private static readonly Regex SequenceRegex = new Regex(@"([A-Z])(?:\[([\w\{\}]+)\])?", RegexOptions.Compiled);

        private IChemicalFormula _cTerminus;
        private IChemicalFormula _nTerminus;
        private IMass[] _modifications;
        private IAminoAcid[] _aminoAcids;
        private string _sequenceWithMods;

        internal bool IsDirty { get; set; }

        internal IMass[] Modifications { get { return _modifications; } }

        #region Constructors

        protected AminoAcidPolymer()
            : this(string.Empty, DefaultNTerminus, DefaultCTerminus) { }

        protected AminoAcidPolymer(string sequence)
            : this(sequence, DefaultNTerminus, DefaultCTerminus) { }

        protected AminoAcidPolymer(string sequence, IChemicalFormula nTerm, IChemicalFormula cTerm)
        {
            MonoisotopicMass = 0;
            int length = sequence.Length;
            _aminoAcids = new IAminoAcid[length];
            _modifications = new IMass[length + 2]; // +2 for the n and c term         
            NTerminus = nTerm;
            CTerminus = cTerm;
            ParseSequence(sequence);      
        }

        protected AminoAcidPolymer(AminoAcidPolymer aminoAcidPolymer, bool includeModifications = true)
            : this(aminoAcidPolymer, 0, aminoAcidPolymer.Length, includeModifications) { }

        protected AminoAcidPolymer(AminoAcidPolymer aminoAcidPolymer, int firstResidue, int length, bool includeModifications = true)
        {
            if (firstResidue < 0 || firstResidue > aminoAcidPolymer.Length)
                throw new IndexOutOfRangeException(string.Format("The first residue index is outside the valid range [{0}-{1}]", 0, aminoAcidPolymer.Length));
            if (length + firstResidue > aminoAcidPolymer.Length)
                length = aminoAcidPolymer.Length - firstResidue;
            Length = length;
            _aminoAcids = new IAminoAcid[length];
            _modifications = new IMass[length + 2];

            MonoisotopicMass = 0.0;
            for (int i = 0; i < Length; i++)
            {
                var aa = aminoAcidPolymer._aminoAcids[i + firstResidue];
                _aminoAcids[i] = aa;
                MonoisotopicMass += aa.MonoisotopicMass;
            }
         
            NTerminus = (firstResidue == 0) ? aminoAcidPolymer.NTerminus : DefaultNTerminus;
            CTerminus = (length + firstResidue == aminoAcidPolymer.Length) ? aminoAcidPolymer.CTerminus : DefaultCTerminus;

            if (includeModifications)
            {
                if (firstResidue == 0)
                    NTerminusModification = aminoAcidPolymer.NTerminusModification;

                if(length + firstResidue == aminoAcidPolymer.Length)
                    CTerminusModification = aminoAcidPolymer.CTerminusModification;
                
                for (int i = 1; i < length; i++)
                {
                    IMass mod = aminoAcidPolymer._modifications[i + firstResidue];
                    if (mod == null)
                        continue;

                    MonoisotopicMass += mod.MonoisotopicMass;
                    _modifications[i] = mod;
                }
            }
            IsDirty = true;
        }

        #endregion
        
        /// <summary>
        /// Gets or sets the C terminus of this amino acid polymer
        /// </summary>        
        public IChemicalFormula CTerminus
        {
            get { return _cTerminus; }
            set
            {
                if (Equals(value, _cTerminus))
                    return;
               
                if (_cTerminus != null)
                    MonoisotopicMass -= _cTerminus.MonoisotopicMass;

                _cTerminus = value;

                if(value != null)
                    MonoisotopicMass += value.MonoisotopicMass;
            }
        }

        /// <summary>
        /// Gets or sets the N terminus of this amino acid polymer
        /// </summary>
        public IChemicalFormula NTerminus
        {
            get { return _nTerminus; }
            set
            {
                if (Equals(value, _nTerminus))
                    return;
          
                if (_nTerminus != null)
                    MonoisotopicMass -= _nTerminus.MonoisotopicMass;

                _nTerminus = value;

                if(value != null)
                    MonoisotopicMass += value.MonoisotopicMass;
            }
        }

        /// <summary>
        /// Gets the number of amino acids in this amino acid polymer
        /// </summary>
        public int Length { get; private set; }
        
        public double MonoisotopicMass { get; private set; }

        #region Amino Acid Sequence

        private string _leucineSequence;
        public string GetLeucineSequence()
        {
            if (IsDirty || string.IsNullOrEmpty(_leucineSequence))
            {
                _leucineSequence =  Sequence.Replace('I', 'L');
            }
            return _leucineSequence;
        }

        public bool Contains(char residue)
        {
            return _aminoAcids.Any(aa => aa.Letter.Equals(residue));
        }

        public bool Contains(IAminoAcid residue)
        {
            return _aminoAcids.Contains(residue);
        }

        /// <summary>
        /// The base amino acid sequence
        /// </summary>
        public string Sequence
        {
            get
            {
                return new string(_aminoAcids.Select(aa => aa.Letter).ToArray());
            }
        }

        /// <summary>
        /// The amino acid sequence with modifications
        /// </summary>
        public string SequenceWithModifications
        {
            get
            {
                if (IsDirty)
                {
                    CleanUp();
                }
                return _sequenceWithMods;
            }
        }

        /// <summary>
        /// Gets the total number of amino acid residues in this amino acid polymer
        /// </summary>
        /// <returns>The number of amino acid residues</returns>
        public int ResidueCount()
        {
            return Length;
        }

        public int ResidueCount(IAminoAcid aminoAcid)
        {
            if (aminoAcid == null)
                return 0;

            return _aminoAcids.Count(aar => aar.Equals(aminoAcid));
        }

        /// <summary>
        /// Gets the number of amino acids residues in this amino acid polymer that
        /// has the specified residue letter
        /// </summary>
        /// <param name="residueChar">The residue letter to search for</param>
        /// <returns>The number of amino acid residues that have the same letter in this polymer</returns>
        public int ResidueCount(char residueChar)
        {
            return _aminoAcids.Count(aar => aar.Letter.Equals(residueChar));
        }

        /// <summary>
        /// Gets the IAminoAcid at the specified position (1-based)
        /// </summary>
        /// <param name="index">The 1-based index of the amino acid to get</param>
        /// <returns>The IAminoAcid at the specified position</returns>
        public IAminoAcid this[int index]
        {
            get
            {
                if (index - 1 > Length || index < 1)
                    throw new IndexOutOfRangeException();
                return _aminoAcids[index - 1];
            }
        }

        #endregion

        #region Fragmentation

        public Fragment Fragment(FragmentTypes type, int number)
        {
            if (type == FragmentTypes.None)
            {
                return null;
            }

            if (number < 1 || number > Length)
            {
                throw new IndexOutOfRangeException();
            }
          
            double monoMass = 0;

            int start = 0;
            int end = number;

            if (type >= FragmentTypes.x)
            {
                start = Length - number;
                end = Length;

                //mass.Add(CTerminus.Mass);
                monoMass += CTerminus.MonoisotopicMass;
                if (CTerminusModification != null)
                {
                    //mass.Add(CTerminusModification.Mass);
                    monoMass += CTerminusModification.MonoisotopicMass;
                }
            }
            else
            {
                //mass.Add(NTerminus.Mass);
                monoMass += NTerminus.MonoisotopicMass;
                if (NTerminusModification != null)
                {
                    //mass.Add(NTerminusModification.Mass);
                    monoMass += NTerminusModification.MonoisotopicMass;
                }
            }

            for (int i = start; i < end; i++)
            {
                //mass.Add(_aminoAcids[i].Mass);
                monoMass += _aminoAcids[i].MonoisotopicMass;
                IMass mod;
                if ((mod = _modifications[i + 1]) != null)
                {
                    monoMass += mod.MonoisotopicMass;
                   // mass.Add(mod.Mass);
                }
            }

            return new Fragment(type, number, monoMass, this);
        }
        
        /// <summary>
        /// Calculates all the fragments of the types you specify
        /// </summary>
        /// <param name="types"></param>
        /// <returns></returns>
        public IEnumerable<Fragment> Fragment(FragmentTypes types)
        {
            return Fragment(types, 1, Length - 1);
        }

        //public IEnumerable<Fragment> Fragment(FragmentTypes types, int number)
        //{
        //    return Fragment(types, number, number);
        //}

        public IEnumerable<Fragment> Fragment(FragmentTypes types, int min, int max)
        {
            if (types == FragmentTypes.None)
            {
                yield break;
            }

            if (min < 1 || max > Length - 1)
            {
                throw new IndexOutOfRangeException();
            }

            foreach (FragmentTypes type in Enum.GetValues(typeof(FragmentTypes)))
            {
                if (type == FragmentTypes.None || type == FragmentTypes.Internal) continue;
                if ((types & type) == type)
                {
                    double monoMass = 0;
                    int start = min;
                    int end = max;

                    IMass mod;
                    if (type >= FragmentTypes.x)
                    {
                        monoMass += CTerminus.MonoisotopicMass;

                        if (CTerminusModification != null)
                        {
                            monoMass += CTerminusModification.MonoisotopicMass;
                        }                           
                        for (int i = end; i >= start; i--)
                        {
                            monoMass += _aminoAcids[i].MonoisotopicMass;
                         
                            if ((mod = _modifications[i + 1]) != null)
                            {
                                monoMass += mod.MonoisotopicMass;
                            }
                            yield return new Fragment(type, Length - i, monoMass, this);
                        }
                    }
                    else
                    {
                        monoMass += NTerminus.MonoisotopicMass;

                        if (NTerminusModification != null)
                        {
                            monoMass += NTerminusModification.MonoisotopicMass;
                        }                            

                        for (int i = start; i <= end; i++)
                        {
                            monoMass += _aminoAcids[i - 1].MonoisotopicMass;

                            if ((mod = _modifications[i]) != null)
                            {
                                monoMass += mod.MonoisotopicMass;
                            }
                            yield return new Fragment(type, i, monoMass, this);
                        }
                    }
                }
            }
        }

        #endregion

        #region Modifications

        /// <summary>
        /// Gets or sets the modification of the C terminus on this amino acid polymer
        /// </summary>        
        public IMass CTerminusModification
        {
            get
            {
                return _modifications[Length + 1];
            }
            set
            {
                ReplaceMod(Length + 1, value);
            }
        }

        /// <summary>
        /// Gets or sets the modification of the C terminus on this amino acid polymer
        /// </summary>        
        public IMass NTerminusModification
        {
            get
            {
                return _modifications[0];
            }
            set
            {
                ReplaceMod(0, value);
            }
        }
        
        /// <summary>
        /// Counts the total number of modifications on this polymer
        /// </summary>
        /// <returns>The number of modifications</returns>
        public int ModificationCount()
        {
            return _modifications.Count(mod => mod != null);
        }

        /// <summary>
        /// Counts the total number of the specified modification on this polymer
        /// </summary>
        /// <param name="modification">The modification to count</param>
        /// <returns>The number of modifications</returns>
        public int ModificationCount(IMass modification)
        {
            if (modification == null)
                return 0;
            
            int count = 0;
            foreach (IMass mod in _modifications)
            {
                if (modification.Equals(mod)) 
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Determines if the specified modification exists in this polymer
        /// </summary>
        /// <param name="modification">The modification to look for</param>
        /// <returns>True if the modification is found, false otherwise</returns>
        public bool Contains(IMass modification)
        {
            if (modification == null)
                return false;

            return _modifications.Contains(modification);
        }

        /// <summary>
        /// Get the modification at the given residue number
        /// </summary>
        /// <param name="residueNumber">The amino acid residue number</param>
        /// <returns>The modification at the site, null if there isn't any modification present</returns>
        public IMass GetModification(int residueNumber)
        {
            if (residueNumber > Length || residueNumber < 1)
            {
                throw new IndexOutOfRangeException(string.Format("Residue number not in the correct range: [{0}-{1}] you specified: {2}", 1, Length, residueNumber));
            }
            return _modifications[residueNumber];
        }

        public bool TryGetModification(int residueNumber, out IMass mod)
        {
            mod = GetModification(residueNumber);
            return mod != null;  
        }

        public bool TryGetModification<T>(int residueNumber, out T mod) where T : class, IMass
        {       
            mod = GetModification(residueNumber) as T;
            return mod != null;
        }

        /// <summary>
        /// Sets the modification at the terminus of this amino acid polymer
        /// </summary>
        /// <param name="mod">The mod to set</param>
        /// <param name="terminus">The termini to set the mod at</param>
        public void SetModification(IMass mod, Terminus terminus)
        {
            if ((terminus & Terminus.N) == Terminus.N)
            {
                NTerminusModification = mod;
            }
            if ((terminus & Terminus.C) == Terminus.C)
            {
                CTerminusModification = mod;
            }
        }

        /// <summary>
        /// Sets the modification at specific sites on this amino acid polymer
        /// </summary>
        /// <param name="mod">The modification to set</param>
        /// <param name="sites">The sites to set the modification at</param>
        /// <returns>The number of modifications added to this amino acid polymer</returns>
        public virtual int SetModification(IMass mod, ModificationSites sites)
        {
            int count = 0;

            if ((sites & ModificationSites.NPep) == ModificationSites.NPep)
            {
                NTerminusModification = mod;
                count++;
            }

            for (int i = 0; i < Length; i++)
            {
                ModificationSites site = _aminoAcids[i].Site;
                if ((sites & site) == site)
                {
                    ReplaceMod(i + 1, mod);
                    count++;
                }
            }

            if ((sites & ModificationSites.PepC) == ModificationSites.PepC)
            {
                CTerminusModification = mod;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Clears the modification set at the terminus of this amino acid polymer back
        /// to the default C or N modifications.
        /// </summary>
        /// <param name="terminus">The termini to clear the mod at</param>
        public void ClearModifications(Terminus terminus)
        {
            if ((terminus & Terminus.N) == Terminus.N)
            {
                NTerminusModification = null;
            }
            if ((terminus & Terminus.C) == Terminus.C)
            {
                CTerminusModification = null;
            }
        }

        public int SetModification(IMass mod, char letter)
        {
            int count = 0;
            for (int i = 0; i < Length; i++)
            {
                if (letter.Equals(_aminoAcids[i].Letter))
                {
                    ReplaceMod(i + 1, mod);
                    count++;
                }
            }

            return count;         
        }



        public int SetModification(IMass mod, IAminoAcid residue)
        {
            int count = 0;
            for (int i = 0; i < Length; i++)
            {
                if (residue.Equals(_aminoAcids[i]))
                {
                    ReplaceMod(i + 1, mod);
                    count++;
                }
            }
            return count;        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="residueNumber">(1-based) residue number</param>
        public void SetModification(IMass mod, int residueNumber)
        {
            if (residueNumber > Length || residueNumber < 1)
            {
                throw new IndexOutOfRangeException(string.Format("Residue number not in the correct range: [{0}-{1}] you specified: {2}", 1, Length, residueNumber));
            }

            ReplaceMod(residueNumber, mod);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="residueNumbers">(1-based) residue number</param>
        public void SetModification(IMass mod, params int[] residueNumbers)
        {
            foreach (int residueNumber in residueNumbers)
            {
                SetModification(mod, residueNumber);
            }
        }
        
        /// <summary>
        /// Clear all modifications from this amino acid polymer.
        /// Includes N and C terminus modifications.
        /// </summary>       
        public void ClearModifications()
        {
            if (ModificationCount() == 0)
                return;

            for (int i = 0; i <= Length + 1; i++)
            {
                if (_modifications[i] == null)
                    continue;
              
                MonoisotopicMass -= _modifications[i].MonoisotopicMass;
                _modifications[i] = null;
                IsDirty = true;
            }
        }

        /// <summary>
        /// Removes the specified mod from all locations on this polymer
        /// </summary>
        /// <param name="mod">The modification to remove from this polymer</param>
        public void ClearModifications(IMass mod)
        {
            if (mod == null)
                return;
         
            for (int i = 0; i <= Length + 1; i++)
            {
                if (!mod.Equals(_modifications[i])) 
                    continue;

                MonoisotopicMass -= mod.MonoisotopicMass;
                _modifications[i] = null;
                IsDirty = true;
            }
        }

        #endregion

        #region ChemicalFormula
       
        public bool TryGetChemicalFormula(out ChemicalFormula formula)
        {
            formula = new ChemicalFormula();

            // Handle Modifications
            for (int i = 0; i < Length + 2; i++)
            {
                IMass mod;
                if ((mod = _modifications[i]) != null)
                {
                    IChemicalFormula chemMod = mod as IChemicalFormula;
                    if (chemMod == null)
                        return false;
                    formula.Add(chemMod.ChemicalFormula);
                }
            }

            // Handle N-Terminus
            formula.Add(NTerminus.ChemicalFormula);

            // Handle C-Terminus
            formula.Add(CTerminus.ChemicalFormula);

            // Handle Amino Acid Residues
            for (int i = 0; i < Length; i++)
            {               
                formula.Add(_aminoAcids[i].ChemicalFormula);
            }           

            return true;
        }

        #endregion

        #region Digestion

        public virtual IEnumerable<Peptide> Digest(IProtease protease, int maxMissedCleavages = 3, int minLength = 1, int maxLength = int.MaxValue)
        {
            return Digest(new [] { protease }, maxMissedCleavages, minLength, maxLength);
        }

        /// <summary>
        /// Digests this amino acid polymer into peptides.
        /// </summary>
        /// <param name="proteases">The proteases to digest with</param>
        /// <param name="maxMissedCleavages">The max number of missed cleavages generated, 0 means no missed cleavages</param>
        /// <param name="minLength">The minimum length (in amino acids) of the peptide</param>
        /// <param name="maxLength">The maximum length (in amino acids) of the peptide</param>
        /// <returns>A list of digested peptides</returns>
        public virtual IEnumerable<Peptide> Digest(IEnumerable<IProtease> proteases, int maxMissedCleavages = 3, int minLength = 1, int maxLength = int.MaxValue)
        {
            if (maxMissedCleavages < 0)
            {
                throw new ArgumentOutOfRangeException("maxMissedCleavages", "The maximum number of missedcleavages must be >= 0");
            }

            string sequence = Sequence;
        
            // Combine all the proteases digestion sites
            SortedSet<int> locations = new SortedSet<int> { -1 };
            foreach (IProtease protease in proteases)
            {
                if (protease != null)
                {
                    locations.UnionWith(protease.GetDigestionSites(sequence));
                }
            }
            locations.Add(sequence.Length - 1);

            IList<int> indices = new List<int>(locations);
            //indices.Sort(); // most likely not needed if locations is a sorted set

            int indiciesCount = indices.Count;
            for (int missedCleavages = 0; missedCleavages <= maxMissedCleavages; missedCleavages++)
            {
                int max = indiciesCount - missedCleavages - 1;
                for (int i = 0; i < max; i++)
                {
                    int len = indices[i + missedCleavages + 1] - indices[i];
                    if (len < minLength || len > maxLength) 
                        continue;

                    int begin = indices[i] + 1;
                    yield return new Peptide(this, begin, len);
                }
            }
        }

        #endregion

        public bool Contains(IAminoAcidSequence item)
        {
            return Contains(item.Sequence);
        }
        
        public bool Contains(string sequence)
        {
            return Sequence.Contains(sequence);
        }

        public override string ToString()
        {
            return SequenceWithModifications;
        }
             
        public override int GetHashCode()
        {
            return MonoisotopicMass.GetHashCode();          
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            AminoAcidPolymer aap = obj as AminoAcidPolymer;
            return aap != null && Equals(aap);
        }

        public bool Equals(AminoAcidPolymer other)
        {
            if (other == null || 
                Length != other.Length || 
                !NTerminus.Equals(other.NTerminus) || 
                !CTerminus.Equals(other.CTerminus))
                return false;
        
            for (int i = 0; i <= Length + 1; i++)
            {
                if (!Equals(_modifications[i], other._modifications[i]))
                    return false;

                if(i == 0 || i == Length + 1)
                    continue; // uneven arrays, so skip these two conditions

                if (!_aminoAcids[i - 1].Equals(other._aminoAcids[i - 1]))
                    return false;
            }
            return true;
        }

        #region Private Methods

        /// <summary>
        /// Replaces a modification (if present) at the specific index in the residue (0-based for N and C termini)
        /// </summary>
        /// <param name="index">The residue index to replace at</param>
        /// <param name="mod">The modification to replace with</param>
        private void ReplaceMod(int index, IMass mod)
        {
            IMass oldMod = _modifications[index]; // Get the mod at the index, if present

            if (Equals(mod, oldMod))
                return; // Same modifications, no change is required

            IsDirty = true;

            if (oldMod != null)
                MonoisotopicMass -= oldMod.MonoisotopicMass; // remove the old mod mass

            _modifications[index] = mod;

            if (mod != null) 
                MonoisotopicMass += mod.MonoisotopicMass; // add the new mod mass
        }

        private void CleanUp()
        {   
            StringBuilder modSeqSb = new StringBuilder(Length);

            IMass mod;    

            // Handle N-Terminus Modification
            if ((mod = _modifications[0]) != null)
            {
                modSeqSb.Append('[');
                modSeqSb.Append(mod);
                modSeqSb.Append("]-");
            }

            // Handle Amino Acid Residues
            for (int i = 0; i < Length; i++)
            {
                IAminoAcid aa = _aminoAcids[i];
              
                char letter = aa.Letter;
                modSeqSb.Append(letter);

                // Handle Amino Acid Modification (1-based)
                if ((mod = _modifications[i + 1]) != null)  
                {
                    modSeqSb.Append('[');
                    modSeqSb.Append(mod);
                    modSeqSb.Append(']');
                }
            }
          
            // Handle C-Terminus Modification
            if ((mod = _modifications[Length + 1]) != null)
            {
                modSeqSb.Append("-[");
                modSeqSb.Append(mod);
                modSeqSb.Append(']');
            }
            
            _sequenceWithMods = modSeqSb.ToString();
            
            IsDirty = false;
        }

        private void ParseSequence(string sequence)
        {
            IsDirty = true;

            if (string.IsNullOrEmpty(sequence))
                return;

            bool inMod = false;
            bool cterminalMod = false; // n or c terminal modification
            int index = 0;          

            StringBuilder modSb = new StringBuilder(10);
            foreach (char letter in sequence)
            {
                if (inMod)
                {
                    if (letter == ']')
                    {
                        inMod = false;
                      
                        string modString = modSb.ToString();
                        modSb.Clear();                   
                        IMass modification;
                        switch (modString)
                        {
                            case "#": // Make the modification unverisally heavy (all C12 and N14s are promoted to C13 and N15s)
                                modification = NamedChemicalFormula.MakeHeavy(_aminoAcids[index - 1]);
                                break;
                            default:
                                NamedChemicalFormula formula;
                                double mass;
                                if (NamedChemicalFormula.TryGetModification(modString, out formula))
                                {
                                    modification = formula;
                                }
                                else if (ChemicalFormula.IsValidChemicalFormula(modString))
                                {
                                    modification = new ChemicalFormula(modString);
                                }
                                else if (double.TryParse(modString, out mass))
                                {
                                    modification = new Mass(mass);
                                }
                                else
                                {
                                    throw new ArgumentException("Unable to correctly parse the following modification: " + modString);
                                }
                                break;
                        }

                        MonoisotopicMass += modification.MonoisotopicMass;

                        if (cterminalMod)
                        {
                            _modifications[index + 1] = modification;
                        }
                        else
                        {
                            _modifications[index] = modification;
                        }

                        cterminalMod = false;
                    }
                    else
                    {
                        modSb.Append(letter);
                    }
                }
                else
                {
                    AminoAcid residue;
                    if (AminoAcid.TryGetResidue(letter, out residue))
                    {
                        _aminoAcids[index++] = residue;
                        MonoisotopicMass += residue.MonoisotopicMass;
                    }                
                    else
                    {
                        if (letter == '[')
                        {
                            inMod = true;
                        }
                        else if (letter == '-')
                        {                      
                            cterminalMod = (index > 0);
                        }
                        else if (letter == ' ')
                        {
                            // allow spaces by just skipping them.
                        }
                        else
                        {
                            throw new ArgumentException(string.Format("Amino Acid Letter {0} does not exist in the Amino Acid Dictionary", letter));
                        }
                    }
                }
            }

            if (inMod)
            {
                throw new ArgumentException("Couldn't find the closing ] for a modification in this sequence: " + sequence);
            }
           
            Length = index;
            Array.Resize(ref _aminoAcids, Length);
            Array.Resize(ref _modifications, Length + 2);          
            IsDirty = true;             
        }

        #endregion

        #region Statics

        public static IEqualityComparer<AminoAcidPolymer> CompareBySequence { get { return new PeptideSequenceComparer(); } }
        
        public static IEnumerable<string> Digest(string sequence, Protease protease, int maxMissedCleavages = 0, int minLength = 1, int maxLength = int.MaxValue)
        {
            return Digest(sequence, new[] { protease }, maxMissedCleavages, minLength, maxLength);
        }

        public static IEnumerable<string> Digest(string sequence, IEnumerable<IProtease> proteases, int maxMissedCleavages = 3, int minLength = 1, int maxLength = int.MaxValue)
        {
            if (maxMissedCleavages < 0)
            {
                throw new ArgumentOutOfRangeException("maxMissedCleavages", "The maximum number of missedcleavages must be >= 0");
            }
                       
            // Combine all the proteases digestion sites
            SortedSet<int> locations = new SortedSet<int> { -1 };
            foreach (IProtease protease in proteases)
            {
                if (protease != null)
                {
                    locations.UnionWith(protease.GetDigestionSites(sequence));
                }
            }
            locations.Add(sequence.Length - 1);

            IList<int> indices = new List<int>(locations);
         

            int indiciesCount = indices.Count;
            for (int missedCleavages = 0; missedCleavages <= maxMissedCleavages; missedCleavages++)
            {
                int max = indiciesCount - missedCleavages - 1;
                for (int i = 0; i < max; i++)
                {
                    int len = indices[i + missedCleavages + 1] - indices[i];
                    if (len >= minLength && len <= maxLength)
                    {
                        int begin = indices[i] + 1;
                        yield return sequence.Substring(begin, len);
                       // yield return new Peptide(this, begin, len);
                    }
                }
            }
        }
 
        public static double GetMass(string sequence)
        {
            double mass = Constants.Water;
            foreach (char letter in sequence)
            {
                AminoAcid residue;
                if (AminoAcid.TryGetResidue(letter, out residue))
                {
                    mass += residue.MonoisotopicMass;
                }
            }
            return mass;
        }

        #endregion

    }

    class PeptideSequenceComparer : IEqualityComparer<AminoAcidPolymer>
    {
        public bool Equals(AminoAcidPolymer aap1, AminoAcidPolymer aap2)
        {
            return aap1.Sequence.Equals(aap2.Sequence);
        }

        public int GetHashCode(AminoAcidPolymer aap)
        {
            return aap.Sequence.GetHashCode();
        }
    }
   
}