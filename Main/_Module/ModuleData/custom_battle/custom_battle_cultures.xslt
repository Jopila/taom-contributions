<?xml version="1.0" encoding="utf-8"?>
<!--
  Custom Battle only (SubModule.xml registers this for CustomGame, after taom_spcultures).
  Culture.aserai is "Haradrim" everywhere (spcultures.xslt, string aom_aserai_name); in the Custom
  Battle faction list it is the Harwan side next to Shaghana, so show it as "Harwan" there.
  Campaigns never load this file.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:output omit-xml-declaration="no" indent="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Culture[@id='aserai']/@name">
		<xsl:attribute name="name">{=aom_aserai_cb_name}Harwan</xsl:attribute>
	</xsl:template>
</xsl:stylesheet>
