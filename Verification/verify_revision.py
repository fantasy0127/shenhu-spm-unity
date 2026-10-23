"""Checks for particle mapping, example timing and prescribed numerical rules."""
from pathlib import Path
import math, re, csv, random
from datetime import datetime
root = Path(__file__).resolve().parents[1]
src = (root/'Assets/Scripts/Calculation/VirtualParticleMapper.cs').read_text()
def constant(name):
    return float(re.search(r'public double '+name+r'\s*=\s*([\deE.+-]+)',src).group(1))
vp=constant('PrimaryParticleVolumeMm3Override')
kg=constant('GeometricVolumeRatioOverride')
rho=constant('ParticleDensityGPerCm3')
volume=constant('SimulationVolumeM3')
coef=1000/rho/vp*volume/kg
cases=[(481.699,18980),(1065.512,41983),(1223.524,48208),(1268.65,49986),(459.807,18117),
       (12.8,504),(182.05,7173),(6.74,266),(130.96,5160),(8.68,342)]
for c,n in cases:
    assert math.floor(c*coef+.5)==n,(c,c*coef,n)
print('PASS: all 10 figure snapshot targets using available concentration precision; coefficient =',format(coef,'.15g'))
assert math.floor(coef+.5)==39
assert '/ geometricRatio;' in src
assert '/ flocScale;' not in src
# Example timestamps and elapsed-time values.
rows=list(csv.DictReader((root/'Assets/StreamingAssets/ExampleData/sh2_spm_sample.csv').open()))
ts=[datetime.fromisoformat(r['timestamp']) for r in rows]
for a,b in zip(ts,ts[1:]): assert (b-a).total_seconds()==5
for r,t in zip(rows,ts): assert abs(float(r['elapsed_hours'])*3600-(t-ts[0]).total_seconds())<1e-8
assert round(120/.1)==1200 and round(5/.1)==50
print('PASS: SH2 demo timestamp/elapsed agreement at 5 s; 1200/50 interpolation steps')
# Editor and quick-test mapping references.
editor=(root/'Assets/Scripts/Editor/SpmMappingSelfTest.cs').read_text()
quick=(root/'Assets/Scripts/Reproducibility/QuickTestRunner.cs').read_text()
assert '39.40124566602242' in editor
assert '39.400' in quick and '39.403' in quick
print('PASS: current editor and quick tests both use revised mapping')
# Stochastic count adjustment and removal without replacement.
rng=random.Random(20261023)
count=100
for _ in range(1000):
    target=40
    gap=abs(target-count)
    expected=gap*(1-math.exp(-2*.1))
    change=math.floor(expected)+(rng.random()<expected-math.floor(expected))
    count += change if count < target else -change
    assert count>=target
assert count==40
items=list(range(100))
for _ in range(60):
    index=rng.randrange(len(items))
    items[index]=items[-1];items.pop()
assert len(items)==len(set(items))==40
print('PASS: gradual adjustment converges without overshoot; random removal without replacement')
# Contact threshold and volume-preserving display size.
d1,d2=.0256,.032
merged=(d1**3+d2**3)**(1/3)
assert math.isclose(merged**3,d1**3+d2**3,rel_tol=1e-12)
assert merged>max(d1,d2)
print('PASS: displayed merge-volume identity')
try:
    from tree_sitter import Language,Parser
    import tree_sitter_c_sharp
except ImportError:
    print('SKIP: C# syntax parsing (install tree-sitter and tree-sitter-c-sharp); no engine checks run')
else:
    parser=Parser(Language(tree_sitter_c_sharp.language()))
    files=list((root/'Assets/Scripts').rglob('*.cs'))
    for f in files:
        tree=parser.parse(f.read_bytes())
        assert not tree.root_node.has_error,f
    print('PASS: C# syntax parse for',len(files),'scripts (not API/type checking)')
print('All available checks passed. Unity/Tuanjie engine validation remains required.')
