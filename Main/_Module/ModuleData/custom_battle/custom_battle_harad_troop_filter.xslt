<?xml version="1.0" encoding="utf-8"?>
<!--
  Custom Battle only (SubModule.xml registers this for CustomGame, after every NPCCharacters node).
  Custom Battle fills each formation slot from every Soldier-occupation character that is not obsolete,
  of the slot's culture and formation class (ArmyCompositionGroupVM, ArmyCompositionItemVM.IsValidUnitItem).
  TAOM reuses Culture.aserai for Harwan and has Shaghana on Culture.shaghana, so the Harwan/Shaghana
  lists also offered vanilla and TAOM soldiers outside the army, such as Jawwal, Beni Zilal, militia and
  caravan guards. This marks every non-hero Aserai/Shaghana character obsolete unless its id starts
  with "harad_" (all TAOM Harad troops: regular, militia, Serpent, Red Serpent, Pezarsan, noble,
  elephant, howdah, mumakil). Lords and heroes are left alone. Campaigns never load this file.
  The same goes for the four minor clans whose troops lords.xslt retags to their side's culture (#957): the
  legion_of_the_betrayed, company_of_the_boar, eleftheroi and brotherhood_of_woods tier troops now sit on Arthedain,
  Umbar, Sturgia and Dunland, and would otherwise be offered in those cultures' Custom Battle slots. They are
  marked obsolete here by id prefix (CustomBattleTroopFilterTests).
  taom_vanilla_troop_obsolete_overrides.xml already hides the main vanilla Aserai line everywhere.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[not(@is_hero='true') and (starts-with(@id, 'legion_of_the_betrayed_tier_') or starts-with(@id, 'company_of_the_boar_tier_') or starts-with(@id, 'eleftheroi_tier_') or starts-with(@id, 'brotherhood_of_woods_tier_'))]">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="NPCCharacter[(@culture='Culture.aserai' or @culture='Culture.shaghana') and not(@is_hero='true') and not(starts-with(@id, 'harad_'))]">
		<xsl:copy>
			<xsl:apply-templates select="@*[name() != 'is_obsolete']"/>
			<xsl:attribute name="is_obsolete">true</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>
</xsl:stylesheet>
