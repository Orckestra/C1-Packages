<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml"
              omit-xml-declaration="yes"
              indent="yes"/>
  <xsl:strip-space elements="*"/>
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()" />
		</xsl:copy>
	</xsl:template>
	<xsl:template match="/configuration/system.web/httpModules">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()" />
			<xsl:if test="not(add[@name='SassHttpModule'])">
				<add name="SassHttpModule" type="Orckestra.Web.Css.Sass.SassHttpModule, Orckestra.Web.Css.Sass" />
			</xsl:if>
		</xsl:copy>
	</xsl:template>
	<xsl:template match="/configuration/system.webServer/modules">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()" />
			<xsl:if test="not(add[@name='SassHttpModule'])">
				<add name="SassHttpModule" type="Orckestra.Web.Css.Sass.SassHttpModule, Orckestra.Web.Css.Sass" />
			</xsl:if>
		</xsl:copy>
	</xsl:template>
	 <xsl:template match="/configuration/appSettings">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()"/>
      <xsl:if test="not(/configuration/appSettings/add[@key='Orckestra.Web.Css.Sass'])">
        <add key="Orckestra.Web.Css.Sass" value="true"/>
      </xsl:if>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>