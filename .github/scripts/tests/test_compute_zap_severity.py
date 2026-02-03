import importlib.util
from pathlib import Path

SAMPLE = """<?xml version="1.0"?>
<alerts>
  <alertitem><riskcode>3</riskcode></alertitem>
  <alertitem><riskcode>2</riskcode></alertitem>
  <alertitem><riskcode>2</riskcode></alertitem>
  <alertitem><riskcode>0</riskcode></alertitem>
</alerts>
"""


def load_module(path: Path):
    spec = importlib.util.spec_from_file_location("compute_zap_severity", str(path))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def test_compute_and_write(tmp_path: Path):
    script = Path(__file__).resolve().parents[1] / "compute_zap_severity.py"
    mod = load_module(script)

    rpt = tmp_path / "zap-report.xml"
    rpt.write_text(SAMPLE, encoding="utf-8")

    counts = mod.compute_counts(rpt)
    assert counts["3"] == 1
    assert counts["2"] == 2
    assert counts["1"] == 0
    assert counts["0"] == 1
    assert counts["total"] == 4

    out = tmp_path / "zap-severity.env"
    mod.write_env(counts, out)
    content = out.read_text(encoding="utf-8")
    assert "ZAP_HIGH=1" in content
    assert "ZAP_TOTAL=4" in content
